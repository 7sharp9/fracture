﻿open System
open System.Net
open Fracture
open Fracture.Sockets
open HttpMachine
open System.Collections.Generic
open System.Diagnostics

let debug (x:UnhandledExceptionEventArgs) =
    Console.WriteLine(sprintf "%A" (x.ExceptionObject :?> Exception))
    Console.ReadLine() |> ignore

System.AppDomain.CurrentDomain.UnhandledException |> Observable.add debug
let shortdate = DateTime.UtcNow.ToShortDateString
open Fracture.HttpServer

let response = "HTTP/1.0 200 OK\r\nContent-Type: text/plain\r\nConnection: Keep-Alive\r\nContent-Length: 12\r\nServer: Fracture\r\n\r\nHello world.\r\n\r\n"
let server = new HttpServer(headers = (fun (headers, keepAlive, svr, s) -> svr.Send(s, response, not keepAlive)), 
                            body = (fun _ -> ()), 
                            requestEnd = fun _ -> ())

server.Start(6667)
printfn "Http Server started"
Console.ReadKey() |> ignore
