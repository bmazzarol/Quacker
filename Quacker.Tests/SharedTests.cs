using Quacker.Tests.Extensions;

namespace Quacker.Tests;

public sealed class SharedTests
{
    [Fact(DisplayName = "All shared code renders correctly")]
    public Task Case1()
    {
        var driver = string.Empty.BuildDriver([]);

        return Verify(driver);
    }

    [Fact(DisplayName = "Multiple methods renders correctly")]
    public Task Case2()
    {
        var driver = """
            using Quacker;

            internal interface IAppender
            {
                void Append(string message);
            }

            namespace SomeNamespace
            {
                public class SomeClass
                {
                    public List<string> Items { get; } = new();
                    
                    void Append(string message)
                    {
                        Items.Add(message);
                    }
                }    
            }

            internal static partial class Test
            {
                public static void AppendTo<[DuckTyped] T>(T appender, string message) where T : IAppender
                {
                    appender.Append(message);
                }
                
                public static void Test()
                {
                    var sb = new StringBuilder();
                    AppendTo(sb, "Hello");
                    
                    var someClass = new SomeNamespace.SomeClass();
                    AppendTo(someClass, "World");
                }
            }
            """.BuildDriver([]);
        return Verify(driver).IgnoreStandardSupportCode();
    }

    [Fact(DisplayName = "Multiple methods renders correctly with single generic parameter")]
    public Task Case3()
    {
        var driver = """
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
                public static void SaveToFile<[Quacker.DuckTyped] T>(T thing)
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

            """.BuildDriver([]);
        return Verify(driver).IgnoreStandardSupportCode();
    }
}
