using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Application.Exceptions
{
    public class BusinessRuleException(string message) : Exception(message);
}
