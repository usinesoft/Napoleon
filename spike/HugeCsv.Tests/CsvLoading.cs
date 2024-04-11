using HugeCsv.DataImport;
using NUnit.Framework;
using NUnit.Framework.Internal;
using System.Diagnostics;
using System.Text.Json;
using HugeCsv.Values;

namespace HugeCsv.Tests
{


    [TestFixture]
    public class TestsWithCsv
    {
        [Test]
        public void Add_and_retrieve_rows()
        {
            var schema = new TableSchema { Columns = { new ColumnInfo("A", KeyValueType.String), new ColumnInfo("B", KeyValueType.SomeInt) } };
            var table = new CompactTable(schema);
            table.AddRow(new IKeyValue[]{ new StringValue("hello"), new IntValue(34)});
            table.AddRow(new IKeyValue[]{ new StringValue("hello"), new IntValue(35)});

            Assert.That(table.LineCount, Is.EqualTo(2));

            var j1 =table.GetItem(0);
            var j2 =table.GetItem(1);
        }


        [Test]
        public void Load_csv()
        {
            var importer = new CsvImporter();
            
            var table = importer.ProcessFile("TestData\\bpi.csv");
            Assert.That(table, Is.Not.Null);

            var item10 = table.GetItem(10);

            var value11 = item10.Root.AsObject()?["Series_reference"]?.GetValue<string>();

            Assert.That(value11, Is.EqualTo("CEPQ.S611"));

        }

        [Test]
        public void Load_json()
        {
            var schema = new TableSchema().AddPrimaryKey("id", KeyValueType.SomeInt)
                .AddColumn("name", KeyValueType.String).AddColumn("size",KeyValueType.SomeFloat);
            
            var importer = new JsonImporter(schema);

            var json1 = """ {"id":12, "name":"cookie", "size":14.5 } """;
            importer.ImportJson(JsonDocument.Parse(json1));

            var json2 = """ {"id":15, "name":"donut", "size":10 } """;
            importer.ImportJson(JsonDocument.Parse(json2));
            
            Assert.That(importer.Table, Is.Not.Null);

            var item1 = importer.Table.GetItem(1);

            var value11 = item1.Root.AsObject()?["name"]?.GetValue<string>();
            Assert.That(value11, Is.EqualTo("donut"));


            var item0 = importer.Table.GetByPrimaryKey(new IntValue(12));
            var value01 = item0.Root.AsObject()?["name"]?.GetValue<string>();
            Assert.That(value01, Is.EqualTo("cookie"));
        }
    }
}
