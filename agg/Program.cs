using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace agg
{
    class Program
    {
        static void Main(string[] args)
        {
            List<IOption> availableOptions = new List<IOption>
            {
                new AggregationPeriodOption(),
                new DelimiterOption(),
                new HeaderSkipOption(),
                new AggregationFunctionOption(),
                new HelpOption(),
                new VersionOption(),
            };

            var argList = args.ToList();
            AggregationOptions opts = new AggregationOptions();

            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];

                var option = availableOptions.FirstOrDefault(o => (("-" + o.ShortName) == arg || ("--" + o.LongName) == arg));
                if (option == null)
                {
                    if (i != args.Length - 1)
                    {
                        Console.Out.WriteLine($"Unknown option {arg}");
                        Environment.ExitCode = 2;
                        return;
                    }

                    opts.Filename = args[i];
                    continue;
                }

                try
                {
                    option.OptionUpdate(argList.GetRange(i, option.ArgsConsumed), opts);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    return;
                }

                i = i + (option.ArgsConsumed - 1);
            }

            if (opts.HelpWanted)
            {
                Console.Out.WriteLine("USAGE: agg [options...] file\n\nOptions:                     Description [Default]");

                IEnumerable<string> optionText = availableOptions.Select(option => $"    -{option.ShortName}  --{ $"{option.LongName} {(option.ArgsConsumed == 2 ? $"<{option.LongName}>"  : "")}",-15}    {option.HelpText}\n");
                Console.Out.Write(string.Join(string.Empty, optionText));

                return;
            }

            if (opts.VersionWanted)
            {
                Console.Out.WriteLine("Version 0.1.0\nCopyright Mitchell T. Paulus 2019");
                return;
            }


            IEnumerable<string> lines; 
            if (!Console.IsInputRedirected)
            {
                //Console.WriteLine("Reading from File...");
                string fullFilename = Path.GetFullPath(opts.Filename);
                lines = File.ReadLines(fullFilename, Encoding.UTF8).Skip(opts.SkipRows);
            }
            else
            {
                //Console.WriteLine("Reading from standard input ...");
                List<string> standardInputLines = new List<string>();

                string line;
                while ((line = Console.In.ReadLine()) != null) standardInputLines.Add(line);

                lines = standardInputLines.Skip(opts.SkipRows);
            }

            List<(DateTime dateTime, List<string> rawData)> allData = new List<(DateTime dateTime, List<string> rawData)>();

            int lineNumber = 1 + opts.SkipRows;
            foreach (string line in lines)
            {
                var fields = line.Split(opts.Delimiter);
                string dateInput = fields[0];
                bool success = DateTime.TryParse(dateInput, out DateTime dateTime);
                if (!success) {Console.WriteLine($"Could not parse the date/time {dateInput} on line {lineNumber}."); Environment.ExitCode = 1; return;}

                List<string> data = fields.ToList().Skip(1).ToList();
                
                allData.Add((dateTime, data));
                lineNumber++;
            }

            // If no lines, then max is set to 0.
            if (!allData.Any()) return;
            int maxFields = allData.Max(tuple => tuple.rawData.Count);
            DateTime minDate = opts.AggregationPeriod.RoundDown(allData.Min(tuple => tuple.dateTime));

            allData.Sort((tuple1, tuple2) => DateTime.Compare(tuple1.dateTime, tuple2.dateTime));

            DateTime currentMinDate = minDate;
            DateTime currentMaxDate = opts.AggregationPeriod.Next(minDate);

            List<List<double>> sums = new List<List<double>>();

            for (int i = 0; i < maxFields; i++)
            {
                sums.Add(new List<double>());
            }

            foreach ((DateTime dateTime, List<string> data) record in allData)
            {
                if (record.dateTime >= currentMaxDate)
                {
                    WriteOutput(opts, currentMinDate, sums);

                    foreach (List<double> doubles in sums)
                    {
                        doubles.Clear();
                    }

                    currentMinDate = currentMaxDate;
                    currentMaxDate = opts.AggregationPeriod.Next(currentMaxDate);
                }

                for (int i = 0; i < record.data.Count; i++)
                {
                    string dataPoint = record.data[i];
                    // Skip fields with whitespace.
                    if (string.IsNullOrWhiteSpace(dataPoint)) continue;

                    bool success = double.TryParse(dataPoint, out double value);
                    if (!success)
                    {
                        Console.WriteLine($"Could not parse the field {dataPoint} in position {i}/{record.data.Count} on row {lineNumber}.");
                        Environment.ExitCode = 3;
                        return;
                    }

                    sums[i].Add(value);
                }
            }
            // Need to write the final output.
            WriteOutput(opts, currentMinDate, sums);
        }

        private static void WriteOutput(AggregationOptions opts, DateTime currentMinDate, List<List<double>> sums)
        {
            List<string> fields = new List<string> { opts.AggregationPeriod.DateText(currentMinDate) };
            fields.AddRange(sums.Select(list => list.Any() ? opts.AggregationFunc(list).ToString("f") : string.Empty));
            Console.Write(string.Join(opts.Delimiter ?? "\t", fields) + "\n");
        }
    }

    public class AggregationPeriodOption : IOption
    {
        public char ShortName => 'p';
        public string LongName => "period";
        public int ArgsConsumed => 2;
        public void OptionUpdate(List<string> args, AggregationOptions options)
        {
            // Single character version
            string input = args[1];
            if (input.Length == 1)
            {
                char periodChar = input[0];
                AggregationPeriod period = AggregationPeriod.AllPeriods.FirstOrDefault(p => p.Flag == periodChar);
                options.AggregationPeriod = period ?? throw new NotImplementedException($"Could not find period corresponding to {periodChar}");
            }
            else
            {
                AggregationPeriod period = AggregationPeriod.AllPeriods.FirstOrDefault(p => string.Equals(p.Description, input, StringComparison.OrdinalIgnoreCase));
                options.AggregationPeriod = period ?? throw new NotImplementedException($"Could not find period corresponding to {input}");
            }
        }

        public string HelpText => "Period of aggregation. Possible values are daily, weekly, monthly, and yearly [daily]";
    }

    public class HeaderSkipOption : IOption
    {
        public char ShortName => 'n';
        public string LongName => "skip";
        public int ArgsConsumed => 2;
        public void OptionUpdate(List<string> args, AggregationOptions options)
        {
            options.SkipRows = int.Parse(args.Last());
        }

        public string HelpText => "Number of header rows to skip. [0]";
    }

    public class DelimiterOption : IOption
    {
        public char ShortName => 'd';
        public string LongName => "delim";
        public int ArgsConsumed => 2;
        public void OptionUpdate(List<string> args, AggregationOptions options)
        {
            options.Delimiter = args.Last();
        }

        public string HelpText => "Delimiter separating fields [whitespace]";
    }

    public class AggregationFunctionOption : IOption
    {
        protected internal readonly List<(string optionName, Func<IEnumerable<double>, double> function)> OptionList = new  List<(string optionName, Func<IEnumerable<double>, double> function)>
        {
            ("sum", Enumerable.Sum),
            ("mean", Enumerable.Average),
            ("count", l => l.Count()),
            ("max", Enumerable.Max),
            ("min", Enumerable.Min)
        };

        public char ShortName => 'a';
        public string LongName => "agg";
        public int ArgsConsumed => 2;

        public void OptionUpdate(List<string> args, AggregationOptions options)
        {
            var selectedFunction = OptionList.FirstOrDefault(tuple => tuple.optionName == args[1]);
            if (selectedFunction.Equals(default((string optionName, Func<IEnumerable<double>, double> function))))
                throw new ArgumentException($"No corresponding aggregation function for {args[1]}.\nAvailable options:\n{string.Join(", ",OptionList.Select(tuple => tuple.optionName))}");

            options.AggregationFunc = selectedFunction.function;
        }

        public string HelpText => $"Aggregation function. Options include {string.Join(", ", OptionList.Select(tuple => tuple.optionName))}. [sum]";
    }

    public class HelpOption : IOption
    {
        public char ShortName => 'h';
        public string LongName => "help";
        public int ArgsConsumed => 1;

        public void OptionUpdate(List<string> args, AggregationOptions options) => options.HelpWanted = true;
        public string HelpText => "Display help and exit";
    }

    public class VersionOption : IOption
    {
        public char ShortName => 'v';
        public string LongName => "version";
        public int ArgsConsumed => 1;
        public void OptionUpdate(List<string> args, AggregationOptions options) => options.VersionWanted = true;
        public string HelpText => "Show version and exit";
    }

    interface IOption
    {
        char ShortName { get; }
        string LongName { get; }
        int ArgsConsumed { get; }
        void OptionUpdate(List<string> args, AggregationOptions options);
        string HelpText { get; }
    }

    public class AggregationOptions
    {
        public AggregationPeriod AggregationPeriod = AggregationPeriod.Daily;
        public string Delimiter = null;
        public bool VersionWanted;
        public bool HelpWanted;
        public string Filename;
        public int SkipRows = 0;
        public Func<IEnumerable<double>, double> AggregationFunc = Enumerable.Sum;
    }

    public class AggregationPeriod
    {
        public static List<AggregationPeriod> AllPeriods { get; } = new List<AggregationPeriod>();

        public static AggregationPeriod Daily { get; } =
            new AggregationPeriod(1,
                "Daily",
                'd',
                date => new DateTime(date.Ticks).Date,
                date => date.AddTicks(TimeSpan.TicksPerDay),
                date => date.ToString("yyyy-MM-dd"));

        public static AggregationPeriod Weekly { get; } =
            new AggregationPeriod(2,
                "Weekly",
                'w',
                date => date,
                date => date.AddTicks(TimeSpan.TicksPerDay * 7),
                date => date.ToString("yyyy-MM-dd"));

        public static AggregationPeriod Monthly { get; } =
            new AggregationPeriod(3,
                "Monthly",
                'm',
                date => new DateTime(date.Year, date.Month, 1),
                date => date.AddMonths(1),
                date => date.ToString("yyyy-MM"));

        public static AggregationPeriod Yearly { get; } =
            new AggregationPeriod(4,
                "Yearly",
                'y',
                date => new DateTime(date.Year, 1, 1),
                date => date.AddYears(1),
                date => date.ToString("yyyy"));

        public readonly int Id;
        public readonly string Description;
        public Func<DateTime, DateTime> RoundDown;
        public Func<DateTime, DateTime> Next;
        public Func<DateTime, string> DateText;
        public char Flag { get; }

        private AggregationPeriod(int id, string description, char flag, Func<DateTime, DateTime> roundDown, Func<DateTime, DateTime> next, Func<DateTime, string> dateText)
        {
            Flag = flag;
            RoundDown = roundDown;
            Next = next;
            DateText = dateText;
            Id = id;
            Description = description;

            AllPeriods.Add(this);
        }
    }
}
