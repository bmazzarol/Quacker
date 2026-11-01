![Quacker](duck-icon.png)

# Quacker

**Duck typing, resolved at compile time.**

A source generator that specialises a generic method for every combination of argument types it is called with, so a member can be invoked on a type parameter the compiler never had to know about.

[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=bmazzarol_Quacker&metric=coverage)](https://sonarcloud.io/summary/new_code?id=bmazzarol_Quacker) [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=bmazzarol_Quacker&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=bmazzarol_Quacker) [![CD Build](https://github.com/bmazzarol/Quacker/actions/workflows/cd-build.yml/badge.svg)](https://github.com/bmazzarol/Quacker/actions/workflows/cd-build.yml) [![Check Markdown](https://github.com/bmazzarol/Quacker/actions/workflows/check-markdown.yml/badge.svg)](https://github.com/bmazzarol/Quacker/actions/workflows/check-markdown.yml)

## Why?

A generic method can only use members the type parameter is known to have. The usual workarounds are an interface constraint, `dynamic`, or reflection, and all three give up compile time safety or performance.

Quacker takes a different route. You mark the type parameter as duck typed, the generator collects every call site, and for each distinct combination of argument types it emits a concrete copy of the method with that type parameter replaced by the actual type. No runtime lookup, no boxing, no interface to satisfy.

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

Then call it with whatever types you have:

```csharp
GenericFactoryThingy.SaveToFile(new Widget { Name = "Widget", Id = 1 });
GenericFactoryThingy.SaveToFile(new Dohicky { Name = "Dohicky", Id = 2 });
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

Define `QUACKER_EXCLUDE_ATTRIBUTES` if your project already provides the `DuckTyped` attribute.

## Install

Quacker is not published to NuGet yet. Build it from source:

```shell
dotnet build Quacker.slnx
```

and reference `Quacker.dll` as a source generator in the consuming project.
