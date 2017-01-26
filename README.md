# flame
A functional static site generator framework

# TODO

Think about making it plugin based, e.g., everything is plugin. Think about how to dynamically load them, install them individually?

Maybe install each via Paket. Then in script:

    #load <...>
    // ...
    process1 >=> process2 >=> process_just_loaded >=> ...
    
## Configuration

Allow configuration via Yaml. Load it as `Map<string, string>` so that plugins can read too. Might need utilities to ease the use of `Map`. Maybe through `fsharpx`.