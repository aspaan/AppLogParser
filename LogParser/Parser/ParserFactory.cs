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
                    parser.SingleLineLog = true;
                    break;
                case "node":
                    parser = new NodeJSLogParser();
                    parser.SingleLineLog = false;
                    break;
            }
            return parser;
        }
    }
}
