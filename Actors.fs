module Actors

open System
open System.IO
open Akkling
open Akka.Actor


module Helpers =
    let (|IsExit|IsContent|) (input: String) =
        if input.ToLower() = "exit" then IsExit else IsContent

    let (|IsValidPath|IsInvalidPath|) (content: String) =
        if File.Exists content then IsValidPath else IsInvalidPath

    let readLine () =
        Console.ReadLine()

    let writeLineInColor (content: String) (color: ConsoleColor) =
        Console.BackgroundColor <- color
        Console.WriteLine content
        Console.ResetColor ()


module Messages =
    type ConsoleWriterMessage =
        | WriteInfo of String
        | WriteError of String

    type TailOperatorMessage =
        | TailInit
        | TailError of ex: exn
        | TailChange

    type TailCoordinatorMessage =
        | StartTail of String * IActorRef<ConsoleWriterMessage> 

    type InputValidatorMessage =
        | ValidateInput of String

    type ConsoleReaderMessage =
        | ReadInput


module Actors =
    open Helpers
    open Messages

    let consoleWriter (context: Actor<ConsoleWriterMessage>) =
        fun (message: ConsoleWriterMessage) ->
            match message with
            | WriteInfo text -> 
                writeLineInColor text ConsoleColor.Green
            | WriteError text ->
                writeLineInColor text ConsoleColor.Red
            
            ignored ()

    let tailOperator (path: String) (consoleWriter: IActorRef<ConsoleWriterMessage>) (context: Actor<TailOperatorMessage>) =
        let stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
        let reader = new StreamReader(stream, Text.Encoding.UTF8)
        let watcher = new FileSystemWatcher( 
                          Path = Path.GetDirectoryName path, 
                          Filter = Path.GetFileName path, 
                          NotifyFilter = (NotifyFilters.FileName ||| NotifyFilters.LastWrite))

        context.Self <! TailInit

        watcher.Error
        |> Event.map (fun ev -> ev.GetException())
        |> Event.add (fun ex -> context.Self <! TailError ex)
        watcher.Changed
        |> Event.add (fun ev -> context.Self <! TailChange)
        watcher.EnableRaisingEvents <- true

        fun (message: TailOperatorMessage) ->
            match message with
            | TailInit ->
                let initialText = reader.ReadToEnd()
                consoleWriter <! WriteInfo initialText
            | TailError ex ->
                consoleWriter <! WriteError ex.Message
            | TailChange ->
                let changeText = reader.ReadToEnd()
                consoleWriter <! WriteInfo changeText
            | LifecycleEvent life ->
                match life with
                | PostStop -> 
                    watcher.Dispose()
                    reader.Dispose()
                    stream.Dispose()
                | _ ->
                    ()

            ignored()

    let tailCoordinator (context: Actor<TailCoordinatorMessage>) =
        fun (message: TailCoordinatorMessage) ->
            match message with
            | StartTail (file, writer) ->                
                tailOperator file writer
                |> actorOf2 
                |> props
                |> spawn context "TailOperator"
                |> ignore

            ignored()

    let inputValidator (consoleWriter: IActorRef<ConsoleWriterMessage>) (tailCoordinator: IActorRef<TailCoordinatorMessage>) (context: Actor<InputValidatorMessage>) =
        fun (message: InputValidatorMessage) ->
            match message with
            | ValidateInput input ->
                match input with
                | IsValidPath -> 
                    consoleWriter <! WriteInfo(sprintf "\"%s\" is a valid file path" input)
                    tailCoordinator <! StartTail(input, consoleWriter)
                | IsInvalidPath ->
                    consoleWriter <! WriteError(sprintf "\"%s\" is not a valid file path" input)
                
            context.Sender() <! ReadInput

            ignored()

    let consoleReader (inputValidator: IActorRef<InputValidatorMessage>) (context: Actor<ConsoleReaderMessage>) =
        fun (message: ConsoleReaderMessage) ->
            match message with
            | ReadInput ->
                let input = readLine()

                match input with
                | IsExit -> 
                    context.System.Terminate() |> ignore
                | IsContent ->
                    inputValidator <! ValidateInput input

            ignored ()