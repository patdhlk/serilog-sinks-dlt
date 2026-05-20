# Contributing

Thanks for considering a contribution. The repo is small enough that the entire dev loop fits on one screen.

## Local dev loop

```sh
# Restore + build all targets (net8.0 + net9.0)
dotnet restore
dotnet build -c Release -warnaserror

# Unit tests (fast — fake daemon, no real socket needed)
dotnet test -c Release --filter "Category!=RealDaemon"

# End-to-end against a real dlt-daemon (uses the Docker dev container)
docker build -f docker/Dockerfile -t serilog-sinks-dlt-dev .
docker run --rm -v "$PWD:/workspace" serilog-sinks-dlt-dev demo.sh
```

The dev container builds `dlt-daemon` from COVESA source with Unix-socket IPC. See [`docker/README.md`](docker/README.md).

## Coding conventions

- Library code (`src/`) compiles with `TreatWarningsAsErrors=true` and `Nullable=enable`. Keep it warning-clean.
- Test code (`tests/`) keeps `TreatWarningsAsErrors=false` so xUnit analyzer hints don't block iteration.
- All non-public types are `internal`; the assembly exposes `InternalsVisibleTo` to `Serilog.Sinks.Dlt.Tests`.
- TDD discipline: when adding behavior, write a failing test first.

## Filing a PR

1. Branch from `main`.
2. Run the full unit suite + the docker `demo.sh` locally before pushing.
3. Open a PR with a one-paragraph summary of *what* and *why*. Link any relevant DLT spec section if you're touching the wire format.

## Releasing (maintainers)

Versioning is driven by git tags. To cut a release:

```sh
git tag v0.2.0
git push origin v0.2.0
```

`.github/workflows/release.yml` picks up the tag, packs with `-p:Version=0.2.0`, and pushes to NuGet using the `NUGET_API_KEY` repo secret. Update `CHANGELOG.md` in the same commit you tag.
