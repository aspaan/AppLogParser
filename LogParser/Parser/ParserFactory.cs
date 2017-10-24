using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogParser
{
    public class ParserFactory
    {
        public static Parser GetParser(string stack)
        {
            Parser parser = null;
            switch (stack.ToLower())
            {
                case "php":
                    parser = new PhpLogParser();
                    break;
                case "node":
                    parser = new NodeJSLogParser();
                    break;
            }
            return parser;
        }

        public static Parser GetLinuxParser(string stack)
        {
            Parser parser = null;
            switch (stack.ToLower())
            {
                case "php":
                    parser = new PhpLogParserLinux();
                    break;
            }
            return parser;
        }
    }
}
