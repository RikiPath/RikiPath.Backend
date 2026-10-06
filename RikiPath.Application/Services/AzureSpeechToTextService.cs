using Microsoft.AspNetCore.Http;
using RikiPath.Application.IServices;
using RikiPath.Application.Responses.SpeechToText;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Application.Services
{
    public class AzureSpeechToTextService : ISpeechToTextService
    {
        public Task<string> ConvertAudioToTextAsync(IFormFile audioFile)
        {
            throw new NotImplementedException();
        }
    }
}
