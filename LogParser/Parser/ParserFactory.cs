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
            }
            return parser;
        }
    }
}
