# /run

> Custom slash command. Invoke with: `/run`
> Starts the project locally using whatever run command fits this repo's
> stack, and verifies it comes up.

## Steps

1. Detect how this project runs by checking, in order: `package.json`
   scripts (`dev`/`start`), a `Makefile` `run` or `up` target, `requirements.txt`
   + an entry point (`app.py`/`main.py`), `Cargo.toml` (`cargo run`),
   `go.mod` (`go run .`), a `.csproj`/`.sln` (`dotnet run`), a
   `Gemfile` (`bin/rails server` or similar). If nothing matches, ask
   the user how they run this project instead of guessing.
2. Check for any required local services this project depends on (e.g.
   a local API, database, or model server mentioned in README/CLAUDE.md)
   before starting, and warn if one isn't reachable.
3. Start the app in the background using the detected command.
4. Wait a moment, then confirm it's accessible at whatever port/URL the
   tool reports.
5. Report the URL to the user and note any startup warnings from the
   process output.

## Guardrails

- If no entry point / run target exists yet, stop and tell the user to
  scaffold the app first.
- If dependencies are missing, suggest the stack's install command
  (`npm install`, `pip install -r requirements.txt`, `cargo build`,
  `dotnet restore`, etc.) rather than assuming one.
