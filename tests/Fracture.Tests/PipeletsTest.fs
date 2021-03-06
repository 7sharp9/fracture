//----------------------------------------------------------------------------
//
// Copyright (c) 2011-2012 Dave Thomas (@7sharp9) Ryan Riley (@panesofglass)
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//    http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//----------------------------------------------------------------------------
module Fracture.Tests.PipeletsTest

open Fracture.Pipelets
open NUnit.Framework
open FsUnit

[<Test>]
[<Explicit>]
let ``test basicRouter should increment once with no attached stages``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, Routers.basicRouter, 1, -1)
    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 1
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test basicRouter should increment twice with one attached stage``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, Routers.basicRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, Routers.basicRouter, 1, -1)

    start ++> finish1 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 2
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test basicRouter should increment twice with two attached stages``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, Routers.basicRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, Routers.basicRouter, 1, -1)
    let finish2 = new Pipelet<int,int>("Finish2", run, Routers.basicRouter, 1, -1)

    start ++> finish1 |> ignore
    start ++> finish2 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 2
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test multicastRouter should increment once for the start and the one attached stage``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, Routers.multicastRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, Routers.multicastRouter, 1, -1)
    let finish2 = new Pipelet<int,int>("Finish2", run, Routers.multicastRouter, 1, -1)

    start ++> finish1 |> ignore
    start ++> finish2 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 3
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test multicastRouter should increment once for the start and both attached stages``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, Routers.multicastRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, Routers.multicastRouter, 1, -1)
    let finish2 = new Pipelet<int,int>("Finish2", run, Routers.multicastRouter, 1, -1)

    start ++> finish1 |> ignore
    start ++> finish2 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 3
    } |> Async.RunSynchronously

[<Test>]
[<Explicit>]
let ``test multicastRouter should increment once for the start and all attached stages``() =
    let counter = ref 0
    let run msg =
        incr counter
        Seq.singleton msg
    
    let start = new Pipelet<int,int>("Start", run, Routers.multicastRouter, 1, -1)
    let finish1 = new Pipelet<int,int>("Finish1", run, Routers.multicastRouter, 1, -1)
    let finish2 = new Pipelet<int,int>("Finish2", run, Routers.multicastRouter, 1, -1)
    let finish3 = new Pipelet<int,int>("Finish3", run, Routers.multicastRouter, 1, -1)

    start ++> finish1 |> ignore
    start ++> finish2 |> ignore
    start ++> finish3 |> ignore

    async {
        start.Post 1
        // Give the post a chance to complete
        do! Async.Sleep 100
        return !counter |> should equal 4
    } |> Async.RunSynchronously
