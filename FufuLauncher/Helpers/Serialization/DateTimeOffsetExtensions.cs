/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
*/
namespace FufuLauncher.Helpers.Serialization
{
    public static class DateTimeOffsetExtensions
    {

        public static DateTimeOffset AdjustOffsetOnly(this DateTimeOffset dateTimeOffset, TimeSpan offset)
        {
            return new DateTimeOffset(dateTimeOffset.DateTime, offset);
        }
    }
}
