﻿module WeakOutputSet

open System
open FsUnit
open Xunit
open FsCheck
open FsCheck.Xunit
open FsIncremental

[<AutoOpen>]
module TestHelpers =        
    /// force garbage collection
    let ensureGC() =
        GC.Collect(3, GCCollectionMode.Forced, true, true)
        GC.WaitForFullGCComplete() |> ignore

    let getRealMemory() =
    let m0 = GC.GetTotalMemory(true)
    let arr : byte[] = Array.zeroCreate 1
    ensureGC()
    let m0 = GC.GetTotalMemory(true)

    let mutable m1 = m0

    let mutable size = 0L
    let mutable res = []
    while m0 < m1 do
        let arr : byte[] = Array.zeroCreate (32 - 24)
        size <- size + 32L + 56L
        res <- arr :: res
        m1 <- GC.GetTotalMemory(true)
    m0 - size

/// a dummy type failing on GetHashCode/Equals to ensure that
/// WeakRef/WeakSet only operate on reference-hashes/equality
type NonEqualObject() as this =    
    let weak = WeakReference<_>(this :> IAdaptiveObject)
    let outputs = WeakOutputSet()

    override x.GetHashCode() = failwith "BrokenEquality.GetHashCode should not be called"
    override x.Equals _o = failwith "BrokenEquality.Equals should not be called"

    interface IAdaptiveObject with
        member x.AllInputsProcessed _ = ()
        member x.InputChanged(_,_) = ()
        member x.Level
            with get () = 0
            and set _ = ()
        member x.Mark() = true
        member x.OutOfDate
            with get () = true
            and set _ = ()
        member x.ReaderCount
            with get () = 0
            and set _ = ()
        member x.Weak = weak
        member x.Outputs = outputs



let relevantSizes = [0;1;2;4;8;9;20;400]

[<Fact>]
let ``[WeakOutputSet] add``() =
    relevantSizes |> List.iter (fun cnt ->
        let set = WeakOutputSet()
    
        let many = Array.init cnt (fun _ -> NonEqualObject())
        for m in many do
            set.Add m |> should be True

        let all = set.Consume()
        all.Length |> should equal many.Length

        for a in all do
            many |> Array.exists (fun m -> Object.ReferenceEquals(m, a)) |> should be True
    )

[<Fact>]
let ``[WeakOutputSet] remove``() =
    relevantSizes |> List.iter (fun cnt ->
        let set = WeakOutputSet()
    
        let many = Array.init cnt (fun _ -> NonEqualObject())
        for m in many do set.Add m |> should be True
        for m in many do set.Remove m |> should be True

        let all = set.Consume()
        all |> should be Empty
    )


[<Fact>]
let ``[WeakOutputSet] actually weak``() =
    relevantSizes |> List.iter (fun cnt ->
        let set = WeakOutputSet()
        let addDead() =
            let many = Array.init cnt (fun _ -> NonEqualObject())
            for m in many do
                set.Add m |> should be True

        addDead()
        ensureGC()
        set.Consume() |> should be Empty
    )