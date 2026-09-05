using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Application.Exceptions
{
    public class AiServiceException(string message, Exception? inner = null)
    : Exception(message, inner);
}
