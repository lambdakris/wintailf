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

    let consoleWriter (context: Actor<_>) (message) =
        match message with
        | WriteInfo text -> 
            writeLineInColor text ConsoleColor.Green
        | WriteError text ->
            writeLineInColor text ConsoleColor.Red
        
        writeLine String.Empty

        ignored ()

    let tailOperator (path: String) (consoleWriter: IActorRef<ConsoleWriterMessage>) (context: Actor<_>) =  
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
                let initContent = reader.ReadToEnd()
                consoleWriter <! WriteInfo(sprintf "INIT CONTENT:\n%s" initContent)
            | TailError ex ->
                let errorMessage = ex.Message
                consoleWriter <! WriteError(sprintf "ERROR MESSAGE:\n%s" errorMessage)
            | TailChange ->
                let newContent = reader.ReadToEnd()
                consoleWriter <! WriteInfo(sprintf "NEW CONTENT:\n%s" newContent)
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

    let tailCoordinator (context: Actor<_>) (message) =
        match message with
        | StartTail (file, writer) ->
            let actor = tailOperator file writer

            actor
            |> props
            |> spawn context "TailOperator"
            |> ignore

        ignored()

    let inputValidator (consoleWriter: IActorRef<ConsoleWriterMessage>) (tailCoordinator: IActorRef<TailCoordinatorMessage>) (context: Actor<_>) (message) =
        match message with
        | ValidateInput input ->
            match input with
            | IsValidPath -> 
                consoleWriter <! WriteInfo(sprintf "'%s' is a valid file path" input)
                tailCoordinator <! StartTail(input, consoleWriter)
            | IsInvalidPath ->
                consoleWriter <! WriteError(sprintf "'%s' is not a valid file path" input)
                context.Sender() <! ReadInput

        ignored()

    let consoleReader (inputValidator: IActorRef<InputValidatorMessage>) (context: Actor<_>) (message) =
        match message with
        | ReadInput ->
            let input = readLine()

            writeLine String.Empty

            match input with
            | IsExit -> 
                context.System.Terminate() |> ignore
            | IsContent ->
                inputValidator <! ValidateInput input

        ignored ()