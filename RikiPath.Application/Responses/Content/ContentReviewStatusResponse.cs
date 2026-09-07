using Domain.Enums;
using RikiPath.Application.DTOs.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RikiPath.Application.Responses.Content
{
    public class ContentReviewStatusResponse
    {
        public int Id { get; set; }
        public ContentEntityType EntityType { get; set; }
        public string Title { get; set; } = string.Empty;
        public ContentStatus Status { get; set; }
        public int ContentAuthorId { get; set; }
        public string? ReviewNote { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public int? ReviewedById { get; set; }
    }
}
