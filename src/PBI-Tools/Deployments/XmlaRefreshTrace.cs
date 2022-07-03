// Copyright (c) Mathias Thierbach
// Licensed under the MIT License. See LICENSE in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using CsvHelper;
using CsvHelper.Configuration;
using Serilog;
using Spectre.Console;
using WildcardMatch;
using AMO = Microsoft.AnalysisServices;
using TOM = Microsoft.AnalysisServices.Tabular;

namespace PbiTools.Deployments
{
	/// <summary>
	/// Creates a <see cref="TOM.Trace"/> alongside a XMLA refresh operation, capturing refresh logs and stats.
	/// The behavior of each instance is controlled via <see cref="PbiDeploymentOptions.RefreshOptions.TraceOptions"/>.
	/// The options object allows to disable tracing altogether, in which case this class is a no-op.
	/// </summary>
    public class XmlaRefreshTrace : IDisposable
    {
        private static readonly ILogger Log = Serilog.Log.ForContext<XmlaRefreshTrace>();

		private readonly PbiDeploymentOptions.RefreshOptions.TraceOptions _options;
		private readonly string _basePath;
		private readonly string _sessionId;
		private readonly bool _processSummary;
		private readonly Stopwatch _stopWatch = new();
		private readonly ConcurrentDictionary<string, RefreshSummaryRecord> _summaryRecords = new();

		private TOM.Trace trace;

        public XmlaRefreshTrace(TOM.Server server, PbiDeploymentOptions.RefreshOptions.TraceOptions options, string basePath)
        {
			_options = options ?? throw new ArgumentNullException(nameof(options));
			_basePath = basePath ?? throw new ArgumentNullException(nameof(basePath));

			if (options.Enabled) {
				this._sessionId = server.SessionID;
				this.trace = server.Traces.Add();
				ConfigureTrace();

				_processSummary = options.Summary != null
					&& options.Summary.Events?.Length > 0
					&& options.Summary.ObjectTypes?.Length > 0
					&& (options.Summary.Console || !String.IsNullOrEmpty(options.Summary.OutPath));
			}
        }

		private void ConfigureTrace()
		{
			foreach (var e in Events)
			{
				var traceEvent = trace.Events.Add(e.Key);
				Array.ForEach(e.Value, col => traceEvent.Columns.Add(col));
			}

			trace.Update(AMO.UpdateOptions.Default, AMO.UpdateMode.CreateOrReplace);

			trace.OnEvent += this.OnTraceEvent;
		}

		private static Dictionary<AMO.TraceEventClass, AMO.TraceColumn[]> Events => new()
		{
			{ AMO.TraceEventClass.ProgressReportBegin, new AMO.TraceColumn[]{
				AMO.TraceColumn.EventSubclass,
				AMO.TraceColumn.TextData,
				//AMO.TraceColumn.IntegerData,
				//AMO.TraceColumn.ProgressTotal,
				AMO.TraceColumn.CurrentTime,
				AMO.TraceColumn.StartTime,
				//AMO.TraceColumn.Duration,
				AMO.TraceColumn.ObjectReference,
				AMO.TraceColumn.ObjectID,
				AMO.TraceColumn.ObjectName,
				AMO.TraceColumn.ObjectPath,
				AMO.TraceColumn.ObjectType,
				AMO.TraceColumn.SessionID
			} },
			{ AMO.TraceEventClass.ProgressReportCurrent, new AMO.TraceColumn[]{
				AMO.TraceColumn.EventSubclass,
				AMO.TraceColumn.TextData,
				AMO.TraceColumn.IntegerData,
				AMO.TraceColumn.ProgressTotal,
				AMO.TraceColumn.CurrentTime,
				AMO.TraceColumn.StartTime,
				//AMO.TraceColumn.Duration,
				AMO.TraceColumn.ObjectReference,
				AMO.TraceColumn.ObjectID,
				AMO.TraceColumn.ObjectName,
				AMO.TraceColumn.ObjectPath,
				AMO.TraceColumn.ObjectType,
				AMO.TraceColumn.SessionID
			} },
			{ AMO.TraceEventClass.ProgressReportEnd, new AMO.TraceColumn[]{
				AMO.TraceColumn.EventSubclass,
				AMO.TraceColumn.TextData,
				AMO.TraceColumn.IntegerData,
				AMO.TraceColumn.ProgressTotal,
				AMO.TraceColumn.CurrentTime,
				AMO.TraceColumn.StartTime,
				AMO.TraceColumn.EndTime,
				AMO.TraceColumn.Duration,
				AMO.TraceColumn.CpuTime,
				AMO.TraceColumn.ObjectReference,
				AMO.TraceColumn.ObjectID,
				AMO.TraceColumn.ObjectName,
				AMO.TraceColumn.ObjectPath,
				AMO.TraceColumn.ObjectType,
				AMO.TraceColumn.SessionID,
				AMO.TraceColumn.Success
			} },
			{ AMO.TraceEventClass.ProgressReportError, new AMO.TraceColumn[]{
				AMO.TraceColumn.EventSubclass,
				AMO.TraceColumn.TextData,
				AMO.TraceColumn.IntegerData,
				AMO.TraceColumn.ProgressTotal,
				AMO.TraceColumn.CurrentTime,
				AMO.TraceColumn.StartTime,
				AMO.TraceColumn.Duration,
				AMO.TraceColumn.ObjectReference,
				AMO.TraceColumn.ObjectID,
				AMO.TraceColumn.ObjectName,
				AMO.TraceColumn.ObjectPath,
				AMO.TraceColumn.ObjectType,
				AMO.TraceColumn.SessionID,
				AMO.TraceColumn.Error,
				AMO.TraceColumn.Severity,
			} },
		};

		public void Start()
        {
			if (trace == null) return;

			_stopWatch.Start();
			trace.Start();
		}

		private void OnTraceEvent(object sender, TOM.TraceEventArgs e)
		{
			if (e.SessionID != _sessionId || e.ObjectType == null || e.ObjectName == null) return;

			Log.Verbose("[{EventClass}|{EventSubclass}] {TextData} ({ObjectType}: {ObjectName})", e.EventClass, e.EventSubclass, e.TextData, e.ObjectType.Name, e.ObjectName);

			// Log
			var eventType = $"{e.EventClass}|{e.EventSubclass}|{e.ObjectType.Name}";

			if (_options.LogEvents.Filter?.Length > 0
				&& _options.LogEvents.Filter.Any(f => f.WildcardMatch(eventType, ignoreCase: true)))
			{
				var record = new CsvTraceEvent
				{
					Elapsed = _stopWatch.Elapsed,
					EventClass = e.EventClass.ToString(),
					EventSubClass = e.EventSubclass.ToString(),
					TextData = e.TextData,
					IntData = e.TryGet(x => x.IntegerData),
					Duration = e.TryGet(x => x.Duration),
					CpuTime = e.TryGet(x => x.CpuTime),
					CurrentTime = e.TryGet(x => x.CurrentTime),
					StartTime = e.TryGet(x => x.StartTime),
					ObjectID = e.ObjectID,
					ObjectName = e.ObjectName,
					ObjectPath = e.ObjectPath,
					ObjectType = e.ObjectType?.Name
				};

				Log.Information("|{Elapsed}| [{EventClass}|{EventSubclass}] \"{TextData}\" Rows:{IntData} ({ObjectType}: {ObjectName})",
                    record.Elapsed,
                    record.EventClass,
                    record.EventSubClass,
                    record.TextData,
					record.IntData,
                    record.ObjectType,
                    record.ObjectName);
			}

			// Summary
			if (_processSummary
				&& e.EventClass == AMO.TraceEventClass.ProgressReportEnd
				&& _options.Summary.Events.Contains(e.EventSubclass.ToString())
				&& _options.Summary.ObjectTypes.Contains(e.ObjectType.Name))
			{
				var record = new RefreshSummaryRecord 
				{
					Elapsed = _stopWatch.Elapsed,
					Event = e.EventSubclass.ToString(),
					RowCount = e.TryGet(x => x.IntegerData),
					Duration = e.TryGet(x => x.Duration),
					CpuTime = e.TryGet(x => x.CpuTime),
					ObjectID = e.ObjectID,
					ObjectName = e.ObjectName,
					ObjectType = e.ObjectType?.Name
				};

				if (e.ObjectReference != null) {
					var xml = new XmlDocument();
					xml.LoadXml(e.ObjectReference);
					record.Table = xml.GetElementsByTagName("Table")?.OfType<XmlNode>().Select(x => x.InnerText).FirstOrDefault();
				}

				var key = $"{record.Event}|{record.ObjectID}";
                _summaryRecords[key] = record;
			}
		}

		public void Dispose()
        {
            if (trace != null)
			{
				try {
					trace.Stop();

					if (_processSummary)
					{
						if (_options.Summary.Console)
						{
							var table = new Table();

							var properties = typeof(RefreshSummaryRecord).GetProperties();
							Array.ForEach(properties, p => table.AddColumn(p.Name));

                            foreach (var entry in _summaryRecords.OrderBy(x => x.Value.Table)
								                                 .ThenBy(x => x.Value.ObjectName)
																 .ThenBy(x => x.Value.Event))
                            {
								table.AddRow(properties.Select(p => $"{p.GetValue(entry.Value)}").ToArray());
                            }

							AnsiConsole.Write(table);
						}

						if (!String.IsNullOrEmpty(_options.Summary.OutPath))
						{
                            string csvPath = Path.Combine(_basePath, _options.Summary.OutPath);
							Log.Debug("Writing summary to file: {Path}", csvPath);

                            using var file = new StreamWriter(csvPath);
							using var csv = new CsvWriter(file, new CsvConfiguration(CultureInfo.InvariantCulture));

							csv.WriteHeader<RefreshSummaryRecord>();
							csv.NextRecord();

                            foreach (var entry in _summaryRecords)
                            {
								csv.WriteRecord(entry.Value);
								csv.NextRecord();
                            }

							Log.Information("Refresh summary written to file: {Path}", csvPath);
						}
					}
				}
				finally {
					trace.Dispose();
					trace = null;
				}
            }
        }
    }

	public class CsvTraceEvent
	{
		public TimeSpan Elapsed { get; set; }
		public string EventClass { get; set; }
		public string EventSubClass { get; set; }
		public string TextData { get; set; }
		public long? IntData { get; set; }
		public long? Duration { get; set; }
		public long? CpuTime { get; set; }
		public DateTime? CurrentTime { get; set; }
		public DateTime? StartTime { get; set; }
		public string ObjectID { get; set; }
		public string ObjectName { get; set; }
		public string ObjectPath { get; set; }
		public string ObjectType { get; set; }
	}

	public class RefreshSummaryRecord
	{
		public string Table { get; set; }
		public string Event { get; set; }
        public string ObjectType { get; set; }
		public string ObjectName { get; set; }
		public string ObjectID { get; set; }
		public TimeSpan Elapsed { get; set; }
		public long? RowCount { get; set; }
		public long? Duration { get; set; }
		public long? CpuTime { get; set; }
    }

	public static class TraceEventExtensions
	{
		public static Nullable<T> TryGet<T>(this TOM.TraceEventArgs e, Func<TOM.TraceEventArgs, T> getter) where T : struct
		{
			try
			{
				return getter(e);
            }
			catch
			{
				return default;
			}
		}
	}
}