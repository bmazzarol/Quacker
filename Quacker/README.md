![Quacker](https://raw.githubusercontent.com/bmazzarol/Quacker/main/duck-small-icon.png)

# Quacker

**Duck typing, resolved at compile time.**

A source generator that specialises a generic method for every combination of argument types it is called with, so a member can be invoked on a type parameter the compiler never had to know about.

## Why?

A generic method can only use members the type parameter is known to have. The
usual workarounds are an interface constraint, `dynamic` or reflection, and all
three give up compile time safety or performance.

Quacker takes a different route. Mark the type parameter as duck typed, the
generator collects every call site, and for each distinct combination of
argument types it emits a concrete copy of the method with the type parameter
replaced by the actual type. No runtime lookup, no boxing, no interface to
satisfy.

## How to use

Mark the type parameter with `[DuckTyped]`:

```csharp
public static void SaveToFile<[DuckTyped] T>(T thing)
    where T : IJsonSerializable
{
    var json = thing.ToJson();
    File.WriteAllText("test.json", json.GetRawText());
}
```

Call it with whatever types you have:

```csharp
SaveToFile(new Widget { Name = "Widget", Id = 1 });
SaveToFile(new Dohicky { Name = "Dohicky", Id = 2 });
```

For each call site the generator emits a concrete method:

```csharp
// generated
public static void SaveToFile(global::Quacker.Tests.Widget thing)
{
    var json = thing.ToJson();
    File.WriteAllText("test.json", json.GetRawText());
}
```

## Install

Quacker is not published to NuGet yet. Build it from source:

```shell
dotnet build Quacker.slnx
```

then reference `Quacker.dll` as a source generator in the consuming project.

Define `QUACKER_EXCLUDE_ATTRIBUTES` if the consuming project already provides its
own `DuckTyped` attribute.

## What it does not do

- It does not resolve members at runtime. Every call site has to be visible to
  the generator at compile time
- It is not a general purpose overload resolver; each distinct argument type
  combination produces its own copy of the method
