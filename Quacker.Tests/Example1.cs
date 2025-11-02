using System.Text.Json;

namespace Quacker.Tests;

public interface IJsonSerializable
{
    JsonElement ToJson();
}

public class Widget
{
    public string Name { get; set; }
    public int Id { get; set; }

    public JsonElement ToJson() => JsonSerializer.SerializeToElement(this);
}

public class Dohicky
{
    public string Name { get; set; }
    public int Id { get; set; }

    public JsonElement ToJson() => JsonSerializer.SerializeToElement(this);
}

public static partial class GenericFactoryThingy
{
    public static void SaveToFile<[DuckTyped] T>(T thing)
        where T : IJsonSerializable
    {
        var json = thing.ToJson();
        var file = File.CreateText("test.json");
        file.Write(json.GetRawText());
        file.Close();
    }
}

public static class TestBed
{
    public static void EntryPoint()
    {
        var widget = new Widget { Name = "Widget", Id = 1 };

        GenericFactoryThingy.SaveToFile(widget);

        var dohicky = new Dohicky { Name = "Dohicky", Id = 2 };

        GenericFactoryThingy.SaveToFile(dohicky);
    }
}
