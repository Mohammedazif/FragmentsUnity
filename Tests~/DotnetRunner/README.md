# Running the test suite without Unity

The parser core is pure C#, so the whole `Tests/Editor` suite runs under a
plain .NET SDK as well as in the Unity Test Runner. Unity ignores this folder
because its name ends with `~`.

## Run the tests

```
dotnet test Tests~/DotnetRunner/Tests/FragmentsUnity.Tests.csproj
```

Sample-file tests are skipped unless `FRAGMENTSUNITY_SAMPLE_DIR` points at a
directory holding `AR520.frag`, `Joyson Model.frag` and
`20210219Architecture.frag`:

```
FRAGMENTSUNITY_SAMPLE_DIR=/path/to/samples dotnet test Tests~/DotnetRunner/Tests/FragmentsUnity.Tests.csproj
```

## Compile-check the Unity layer

`Runtime/Unity` and `Editor` need UnityEngine, so they are compiled against
the hand-written API stubs in `UnityStubs/UnityStubs.cs`:

```
dotnet build Tests~/DotnetRunner/UnityStubs/FragmentsUnity.UnityLayer.csproj
```

This verifies syntax, every cross-file contract in the package, and every
reference into `Runtime/Core`, with warnings treated as errors. It does **not**
verify that the Unity API calls themselves are correct — the stub signatures
encode this package's belief about the 2021.3 API, so a real editor is still
the authority. Keep the stubs in sync when the Unity layer starts using a new
API: add the member with its real signature rather than loosening the stub.

## Test the Unity layer against the stubs

A second suite exercises the behaviour of `Runtime/Unity` — asset
serialization, metadata queries and scene assembly — against those same stubs:

```
dotnet test Tests~/DotnetRunner/UnityLayerTests/FragmentsUnity.UnityLayerTests.csproj
```

Its test files live beside the project rather than in `Tests/Editor`, because
they reference `UnityEngine` types that only exist as stubs here. What the
stubs cannot show — parent/child links, component state, mesh contents — stays
untested; those assertions need the Unity Test Runner.
