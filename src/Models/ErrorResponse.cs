using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LegendaryLibraryNS.Models
{
    public class ErrorResponse
    {
        public string errorCode;
        public string errorMessage;
        public int numericErrorCode;
        public string originatingService;
        public string intent;
    }
}
