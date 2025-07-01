using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using J = Newtonsoft.Json.JsonPropertyAttribute;

namespace MaliOC.Core
{
    [Serializable]
    public class Report
    {
        [J("producer")] public Producer Producer;
        [J("schema")] public Schema Schema;
        [J("shaders")] public ShaderElement[] Shaders;
    }

    [Serializable]
    public class Producer
    {
        [J("build")] public string Build;
        [J("documentation")] public Uri Documentation;
        [J("name")] public string Name;
        [J("version")] public long[] Version;
    }

    [Serializable]
    public class Schema
    {
        [J("name")] public string Name;
        [J("version")] public long Version;
    }

    [Serializable]
    public class ShaderElement
    {
        [J("driver")] public string Driver;
        [J("filename")] public string Filename;
        [J("hardware")] public Hardware Hardware;
        [J("attribute_streams")] public AttributeStreams AttributeStreams;
        [J("properties")] public ShaderProperty[] Properties;
        [J("shader")] public ShaderShader Shader;
        [J("variants")] public Variant[] Variants;
        [J("notes")] public string[] Notes;
        [J("warnings")] public string[] Warnings;
        [J("errors")] public string[] Errors;
    }

    [Serializable]
    public class Hardware
    {
        [J("architecture")] public string Architecture;
        [J("core")] public string Core;
        [J("pipelines")] public Pipeline[] Pipelines;
        [J("revision")] public string Revision;
    }

    [Serializable]
    public class Pipeline
    {
        [J("description")] public string Description;
        [J("display_name")] public string DisplayName;
        [J("name")] public string Name;
    }

    [Serializable]
    public class AttributeStreams
    {
        [J("nonposition")] public AttributeStream[] NonPosition;
        [J("position")] public AttributeStream[] Position;
    }

    [Serializable]
    public class AttributeStream
    {
        [J("location")] public int Location;
        [J("symbol")] public string Symbol;
    }

    [Serializable]
    public class ShaderProperty
    {
        [J("description")] public string Description;
        [J("display_name")] public string DisplayName;
        [J("name")] public string Name;
        [J("value")] public bool Value;
    }

    [Serializable]
    public class ShaderShader
    {
        [J("api")] public string Api;
        [J("type")] public string Type;
    }

    [Serializable]
    public class Variant
    {
        [J("name")] public string Name;
        [J("performance")] public Performance Performance;
        [J("properties")] public VariantProperty[] Properties;
    }

    [Serializable]
    public class Performance
    {
        [J("longest_path_cycles")] public Cycles LongestPathCycles;
        [J("pipelines")] public string[] Pipelines;
        [J("shortest_path_cycles")] public Cycles ShortestPathCycles;
        [J("total_cycles")] public Cycles TotalCycles;
    }

    [Serializable]
    public class Cycles
    {
        [J("bound_pipelines")] public string[] BoundPipelines;
        [J("cycle_count")] public double[] CycleCount;
    }

    [Serializable]
    public class VariantProperty
    {
        [J("description")] public string Description;
        [J("display_name")] public string DisplayName;
        [J("name")] public string Name;
        [J("value")] public Value Value;
    }

    [Serializable]
    public struct Value
    {
        public bool? Bool;
        public long? Integer;

        public static implicit operator Value(bool Bool) => new Value { Bool = Bool };
        public static implicit operator Value(long Integer) => new Value { Integer = Integer };
    }

    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling = DateParseHandling.None,
            Converters =
            {
                ValueConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    internal class ValueConverter : JsonConverter
    {
        public override bool CanConvert(Type t) => t == typeof(Value) || t == typeof(Value?);

        public override object ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    var integerValue = serializer.Deserialize<long>(reader);
                    return new Value { Integer = integerValue };
                case JsonToken.Boolean:
                    var boolValue = serializer.Deserialize<bool>(reader);
                    return new Value { Bool = boolValue };
            }

            throw new Exception("Cannot unmarshal type Value");
        }

        public override void WriteJson(JsonWriter writer, object untypedValue, JsonSerializer serializer)
        {
            var value = (Value)untypedValue;
            if (value.Integer != null)
            {
                serializer.Serialize(writer, value.Integer.Value);
                return;
            }

            if (value.Bool != null)
            {
                serializer.Serialize(writer, value.Bool.Value);
                return;
            }

            throw new Exception("Cannot marshal type Value");
        }

        public static readonly ValueConverter Singleton = new ValueConverter();
    }
}