/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FufuLauncher.Helpers.Serialization
{
    public sealed class SimpleDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
    {
        private const string Format = "yyyy-MM-dd HH:mm:ss";

        public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.GetString() is { } dataTimeString)
            {
                DateTime dateTime = DateTime.ParseExact(dataTimeString, Format, CultureInfo.InvariantCulture);
                return new DateTimeOffset(dateTime, default);
            }

            return default;
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.DateTime.ToString(Format, CultureInfo.InvariantCulture));
        }
    }
}
