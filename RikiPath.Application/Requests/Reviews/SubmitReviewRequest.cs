using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Application.Requests.Reviews
{
    public class SubmitReviewRequest
    {
        [Required]
        public int ReviewItemId { get; set; }

        [Range(1, 5, ErrorMessage = "Quality phải trong khoảng 1-5.")]
        public int Quality { get; set; }
    }
}
