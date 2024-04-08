using System.Globalization;

namespace HugeCsv.Values
{
    internal static class ValueFactory
    {
        public static IKeyValue Parse(string valueAsString)
        {
            var type = TypeHint();

            return type switch
            {
                KeyValueType.String when valueAsString == "null" => new NullValue(),

                KeyValueType.String when bool.TryParse(valueAsString, out var bv) => new BoolValue(bv),

                KeyValueType.String => new StringValue( valueAsString.Trim('\'', '"')),

                KeyValueType.SomeFloat when double.TryParse(valueAsString, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var fv) => new FloatValue(fv),

                KeyValueType.SomeFloat => new StringValue(valueAsString), // was not really a float

                KeyValueType.Date when DateTime.TryParse(valueAsString,CultureInfo.InvariantCulture, out var dt) => new DateValue(dt),

                KeyValueType.Date => new StringValue(valueAsString), // was not really a date

                KeyValueType.SomeInt when int.TryParse(valueAsString, out var vi) => new IntValue(vi),

                _ => new StringValue(valueAsString)
            };

            // Try to identify the type
            KeyValueType TypeHint()
            {
                var type = KeyValueType.SomeInt;

                var firstPosition = true;
                // try an educated guess to avoid useless TryParse
                foreach (var c in valueAsString)
                {
                    if (char.IsLetter(c) || c == '\'') // strings may be quoted or not
                    {
                        type = KeyValueType.String;
                        break;
                    }

                    if (!firstPosition && c is '-' or '/')
                    {
                        type = KeyValueType.Date;
                        break;
                    }

                    if (c is '.' or ',')
                    {
                        type = KeyValueType.SomeFloat;
                        break;
                    }

                    firstPosition = false;
                }

                return type;
            }
        }
    }
}
