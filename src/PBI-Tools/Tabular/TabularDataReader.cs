// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Data;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AnalysisServices.AdomdClient;
using Serilog;
using System.IO;
using CsvHelper;

namespace PbiTools.Tabular
{

    public class TabularDataReader : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<TabularDataReader>();
        private AdomdConnection _connection;

        private static readonly Regex colNameRegex = new Regex(@"([^\[]+)\[([^\]]+)");


        public TabularDataReader(string connectionString)
        {
            this._connection = new AdomdConnection(connectionString);
            this._connection.Open();
        }

        public IEnumerable<TResult> ExecuteQuery<TResult>(string query, Func<IDataReader, TResult> rowMapper)
        {
            using (var cmd = _connection.CreateCommand())
            {
                cmd.CommandText = query;
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        yield return rowMapper(reader);
                    }
                }
            }
        }

        public string[] GetTableNames()
        {
            return ExecuteQuery(
                @"select * from $SYSTEM.TMSCHEMA_TABLES", 
                r => r.GetString(r.GetOrdinal("Name"))
            )
            .ToArray(/*Ensures reader is closed*/);
        }

        public void ExtractTableData(string outPath, string dateTimeFormat)
        {
            Directory.CreateDirectory(outPath);
            
            foreach (var table in GetTableNames())
            {
                var path = Path.Combine(outPath, $"{table}.csv");

                Log.Debug("Extracting table {Table} to file: {Path}", table, path);

                using (var outFile = File.CreateText(path))
                using (var csv = new CsvWriter(outFile, CultureInfo.CurrentCulture))
                using (var cmd = _connection.CreateCommand())
                {
                    csv.Context.TypeConverterOptionsCache.GetOptions<DateTime>().Formats = new[] { dateTimeFormat };

                    cmd.CommandText = $"EVALUATE '{table}'";

                    try
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            // Header
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                var column = colNameRegex.Match(reader.GetName(i)).Groups[2].Value ?? reader.GetName(i);
                                csv.WriteField(column);
                            }
                
                            // Data
                            var recordsRead = 0;
                            while (reader.Read())
                            {
                                csv.NextRecord();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    csv.WriteField(reader.GetValue(i));
                                }
                                recordsRead++;
                            }

                            Log.Information("Extracted {RecordCount} records from table {Table}", recordsRead, table);
                        }
                    }
                    catch (AdomdErrorResponseException ex)
                    {
                        Log.Debug(ex, "An error occurred reading from table {Table}", table);
                    }
                }

            }
        }

        public void Dispose()
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }
    }

}