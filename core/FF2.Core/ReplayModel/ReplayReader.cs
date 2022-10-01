using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FF2.Core.ReplayModel
{
    class InvalidReplayException : Exception
    {
        public InvalidReplayException(string message) : base(message) { }

        public int? LineNumber { get; set; }
        public int? ColumnNumber { get; set; }
        public string? LineContent { get; set; }
    }

    public sealed class ReplayReader
    {
        private static readonly char[] emptyLine = new char[0];
        const int maxLineLength = 128;

        private readonly TextReader replayFile;
        private readonly IReplayParser parser;
        private int lineNumber = 0;

        private ReplayReader(TextReader replayFile, IReplayParser parser)
        {
            this.replayFile = replayFile;
            this.parser = parser;
        }

        private static void Read(string filename, IReplayParser parser)
        {
            using (var reader = new StreamReader(filename))
            {
                new ReplayReader(reader, parser).Read();
            }
        }

        public static IReplayDriver FindBestCombo(string filename, TickCalculations tickCalculations)
        {
            var puzzle = GetPuzzles(filename).OrderByDescending(x => x.Combo.AdjustedGroupCount).First();
            return PuzzleReplayDriver.BuildPuzzleReplay(puzzle, tickCalculations);
        }

        public static ReplayDriver BuildReplayDriver(string filename, TickCalculations tickCalculations)
        {
            var parser = new ReplayParser();
            Read(filename, parser);
            return parser.BuildReplayDriver(tickCalculations);
        }

        public static IReadOnlyList<ComboDistillery.Puzzle> GetPuzzles(string filename)
        {
            var parser = new ReplayParser();
            Read(filename, parser);
            return parser.GetPuzzles();
        }

        public int Read()
        {
            lineNumber = 0;
            while (ReadLine(out var line))
            {
                HandleLine(ref line);
            }
            return lineNumber;
        }

        private bool ReadLine(out BufferedLine line)
        {
            lineNumber++;
            var str = replayFile.ReadLine();
            if (str == null)
            {
                line = new BufferedLine(emptyLine, lineNumber);
                return false;
            }
            else if (str.Length > maxLineLength)
            {
                throw new InvalidReplayException("Too many characters on this line")
                {
                    LineNumber = lineNumber,
                    LineContent = str.Substring(0, maxLineLength),
                };
            }
            else
            {
                line = new BufferedLine(str.ToCharArray(), lineNumber);
                return true;
            }
        }

        private void HandleLine(ref BufferedLine line)
        {
            try
            {
                DoHandleLine(ref line);
            }
            catch (InvalidReplayException ex)
            {
                line.AddLineInfo(ex);
                throw;
            }
        }

        private void DoHandleLine(ref BufferedLine line)
        {
            if (TryConsumeLiteral(ref line, "c "))
            {
                var command = (Command)ConsumeInt(ref line);
                var millis = ConsumeInt(ref line);
                parser.Parse(new CommandElement(command, new Moment(millis)));
            }
            else if (TryConsumeLiteral(ref line, "s "))
            {
                string name = ConsumeString(ref line);
                string value = ConsumeString(ref line);
                parser.Parse(new SettingsElement(name, value));
            }
            else if (TryConsumeLiteral(ref line, "version "))
            {
                parser.Parse(new VersionElement(ConsumeInt(ref line)));
            }
        }

        static bool TryConsumeLiteral(ref BufferedLine line, string pattern)
        {
            int len = pattern.Length;
            if (len > line.Length)
            {
                return false;
            }

            for (int i = 0; i < len; i++)
            {
                if (line[i] != pattern[i])
                {
                    return false;
                }
            }

            line = line.Consume(len);
            return true;
        }

        char[] stringBuilderArray = new char[maxLineLength];

        private string ConsumeString(ref BufferedLine line)
        {
            int cursor = 0;

            while (cursor < line.Length && line[cursor] != ' ')
            {
                stringBuilderArray[cursor] = line[cursor];
                cursor++;
            }

            int length = cursor;

            // consume trailing whitespace
            while (cursor < line.Length && line[cursor] == ' ')
            {
                cursor++;
            }

            line = line.Consume(cursor);
            return new string(stringBuilderArray, 0, length);
        }

        private int ConsumeInt(ref BufferedLine line)
        {
            int val = 0;
            bool okay = false;
            bool negative = false;
            int cursor = 0;

            if (cursor < line.Length && line[cursor] == '-')
            {
                negative = true;
                cursor++;
            }

            while (cursor < line.Length)
            {
                int adder = line[cursor] - '0';
                if (adder >= 0 && adder <= 9)
                {
                    okay = true;
                    val *= 10;
                    val += adder;
                    cursor++;
                }
                else
                {
                    break;
                }
            }

            const string errorMessage = "Expected an integer";
            if (!okay)
            {
                throw line.MakeException(errorMessage);
            }

            if (cursor < line.Length)
            {
                if (line[cursor] == ' ')
                {
                    // Consume the trailing space
                    cursor++;
                }
                else
                {
                    // Anything other than a space is not allowed to follow an integer
                    throw line.MakeException(errorMessage);
                }
            }

            line = line.Consume(cursor);
            if (negative)
            {
                val = -val;
            }
            return val;
        }

        readonly ref struct BufferedLine
        {
            private readonly ReadOnlySpan<char> line;
            private readonly int lineNumber;
            private readonly int columnNumber;

            public BufferedLine(ReadOnlySpan<char> line, int lineNumber, int columnNumber = 0)
            {
                this.line = line;
                this.lineNumber = lineNumber;
                this.columnNumber = columnNumber;
            }

            public int Length => line.Length;

            public char this[int index] => line[index];

            public BufferedLine Consume(int length)
            {
                return new BufferedLine(line.Slice(length), lineNumber, columnNumber + length);
            }

            public InvalidReplayException AddLineInfo(InvalidReplayException ex)
            {
                ex.LineNumber = this.lineNumber;
                ex.ColumnNumber = this.columnNumber;
                ex.LineContent = new string(line.ToArray());
                return ex;
            }

            public InvalidReplayException MakeException(string message)
            {
                return AddLineInfo(new InvalidReplayException(message));
            }

            public override string ToString()
            {
                return new string(line.ToArray());
            }
        }
    }
}
