﻿namespace Fracture

open System
open System.Diagnostics
open System.Net
open System.Net.Sockets
open System.Collections.Generic
open System.Collections.Concurrent
open SocketExtensions
open Common

///Creates a new TcpServer using the specified parameters
type TcpServer(poolSize, perOperationBufferSize, acceptBacklogCount, received, ?connected, ?disconnected, ?sent) as s=
    let bocketPool = new BocketPool("regular pool", max poolSize 2, perOperationBufferSize)
    let connectionPool = new BocketPool("connection pool", max (acceptBacklogCount * 2) 2, perOperationBufferSize)(*Note: 288 bytes is the minimum size for a connection*)
    let clients = new ConcurrentDictionary<_,_>()
    let connections = ref 0
    let createSocket() = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    let listeningSocket = createSocket()
    let disposed = ref false
       
    /// Ensures the listening socket is shutdown on disposal.
    let cleanUp disposing = 
        if not !disposed then
            if disposing then
                if listeningSocket <> null then
                    closeConnection listeningSocket
                bocketPool.Dispose()
                connectionPool.Dispose()
            disposed := true

    ///This function is called on each connect,sends,receive, and disconnect
    let rec completed (args:SocketAsyncEventArgs) =
        try
            match args.LastOperation with
            | SocketAsyncOperation.Receive -> processReceive(args)
            | SocketAsyncOperation.Send -> processSend(args)
            | SocketAsyncOperation.Disconnect -> processDisconnect(args)
            | _ -> () // Don't handle AcceptAsync; processAccept handles all accept messages.
        finally
            args.AcceptSocket <- null
            bocketPool.CheckIn(args)

    and processReceive (args) =
        if args.SocketError = SocketError.Success && args.BytesTransferred > 0 then
            //process received data, check if data was given on connection.
            let data = acquireData args
            //trigger received
            received (data, s, args.AcceptSocket)
            //get on with the next receive
            if args.AcceptSocket.Connected then 
                let next = bocketPool.CheckOut()
                next.AcceptSocket <- args.AcceptSocket
                args.AcceptSocket.ReceiveAsyncSafe(completed, next)
        //0 byte receive - disconnect.
        else
            // TODO: Investigate this because it this looks odd. We want to disconnect when no bytes are received?
            let closeArgs = bocketPool.CheckOut()
            closeArgs.AcceptSocket <- args.AcceptSocket
            args.AcceptSocket.Shutdown(SocketShutdown.Both)
            args.AcceptSocket.DisconnectAsyncSafe(completed, closeArgs)

    and processSend (args) =
        match args.SocketError with
        | SocketError.Success ->
            let sentData = acquireData args
            //notify data sent
            sent |> Option.iter (fun x  -> x (sentData, args.AcceptSocket.RemoteEndPoint))
        | SocketError.NoBufferSpaceAvailable
        | SocketError.IOPending
        | SocketError.WouldBlock ->
            failwith "Buffer overflow or send buffer timeout" //graceful termination?  
        | _ -> args.SocketError.ToString() |> printfn "socket error on send: %s"

    and processDisconnect (args) =
        // NOTE: With a socket pool, the number of active connections could be calculated by the difference of the sockets in the pool from the allowed connections.
        !-- connections
        disconnected |> Option.iter (fun x -> x args.AcceptSocket.RemoteEndPoint)
        // TODO: return the socket to the socket pool for reuse.
        // All calls to DisconnectAsync should have shutdown the socket.
        // Calling connectionClose here would just duplicate that effort.
        args.AcceptSocket.Close()
    
    let rec processAccept (args: SocketAsyncEventArgs) =
        match args.SocketError with
        | SocketError.Success ->
            let acceptSocket = args.AcceptSocket
            try
                //start next accept
                let saea = connectionPool.CheckOut()
                listeningSocket.AcceptAsyncSafe(processAccept, saea)
    
                //process newly connected client
                clients.AddOrUpdate(acceptSocket.RemoteEndPoint, acceptSocket, fun _ _ -> acceptSocket) |> ignore
                //if not success then failwith "client could not be added"
    
                //trigger connected
                connected |> Option.iter (fun x  -> x acceptSocket.RemoteEndPoint)
                !++ connections
    
                //start receive on accepted client
                let receiveSaea = bocketPool.CheckOut()
                receiveSaea.AcceptSocket <- acceptSocket
                acceptSocket.ReceiveAsyncSafe(completed, receiveSaea)
    
                //check if data was given on connection
                if args.BytesTransferred > 0 then
                    let data = acquireData args
                    //trigger received
                    in received (data, s, acceptSocket)
            finally
                // remove the AcceptSocket because we're reusing args
                args.AcceptSocket <- null
                connectionPool.CheckIn(args)
        
        | SocketError.OperationAborted
        | SocketError.Disconnecting when !disposed -> () // stop accepting here, we're being shutdown.
        | _ -> Debug.WriteLine (sprintf "socket error on accept: %A" args.SocketError)

    /// PoolSize=10k, Per operation buffer=1k, accept backlog=10000
    static member Create(received, ?connected, ?disconnected, ?sent) =
        new TcpServer(30000, 1024, 10000, received, ?connected = connected, ?disconnected = disconnected, ?sent = sent)

    member s.Connections = connections

    ///Starts the accepting a incoming connections.
    member s.Listen(address: IPAddress, port) =
        //initialise the bocketPool
        bocketPool.Start(completed)
        connectionPool.Start(processAccept)
        ///starts listening on the specified address and port.
        //This disables nagle
        //listeningSocket.NoDelay <- true 
        listeningSocket.Bind(IPEndPoint(address, port))
        listeningSocket.Listen(acceptBacklogCount)
        for i in 1 .. acceptBacklogCount do
            listeningSocket.AcceptAsyncSafe(processAccept, connectionPool.CheckOut())

    ///Sends the specified message to the client.
    member s.Send(clientEndPoint, msg, keepAlive) =
        let success, client = clients.TryGetValue(clientEndPoint)
        if success then 
            send client completed bocketPool.CheckOut perOperationBufferSize msg keepAlive
        else failwith "could not find client %"
        
    member s.Dispose() = (s :> IDisposable).Dispose()

    override s.Finalize() = cleanUp false
        
    interface IDisposable with 
        member s.Dispose() =
            cleanUp true
            GC.SuppressFinalize(s)
