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

    let writeLine (text: String) =
        Console.WriteLine(text)

    let writeLineInColor (text: String) (color: ConsoleColor) =
        Console.BackgroundColor <- color
        Console.WriteLine(text)
        Console.ResetColor()


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
        watcher.Error
        |> Event.map (fun ev -> ev.GetException())
        |> Event.add (fun ex -> context.Self <! TailError ex)
        watcher.Changed
        |> Event.add (fun ev -> context.Self <! TailChange)

        context.Self <! TailInit

        watcher.EnableRaisingEvents <- true

        let rec loop () = actor {
            let! message = context.Receive()

            match message with
            | TailInit ->
                let initialText = reader.ReadToEnd()
                consoleWriter <! WriteInfo(sprintf "Initial file contents:\n%s" initialText)
            | TailError ex ->
                consoleWriter <! WriteError(sprintf "Encountered error:\n%s" ex.Message)
            | TailChange ->
                let changeText = reader.ReadToEnd()
                consoleWriter <! WriteInfo(sprintf "Change in file contents:\n%s" changeText)
            | LifecycleEvent life ->
                match life with
                | PostStop -> 
                    watcher.Dispose()
                    reader.Dispose()
                    stream.Dispose()
                | _ ->
                    ()
            return! loop()
        }
        loop()

    let tailCoordinator (context: Actor<TailCoordinatorMessage>) =
        fun (message: TailCoordinatorMessage) ->
            match message with
            | StartTail (file, writer) ->                
                let actor = tailOperator file writer

                actor
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
                    consoleWriter <! WriteInfo(sprintf "\"%s\" is a valid file path\n" input)
                    tailCoordinator <! StartTail(input, consoleWriter)
                | IsInvalidPath ->
                    consoleWriter <! WriteError(sprintf "\"%s\" is not a valid file path\n" input)
                
            context.Sender() <! ReadInput

            ignored()

    let consoleReader (inputValidator: IActorRef<InputValidatorMessage>) (context: Actor<ConsoleReaderMessage>) =
        fun (message: ConsoleReaderMessage) ->
            match message with
            | ReadInput ->
                writeLine "Enter the path to a file to begin tailing:"

                let input = readLine()

                match input with
                | IsExit -> 
                    context.System.Terminate() |> ignore
                | IsContent ->
                    inputValidator <! ValidateInput input

            ignored ()