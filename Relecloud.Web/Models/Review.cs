﻿using System;
using System.ComponentModel.DataAnnotations;

namespace Relecloud.Web.Models
{
    public class Review
    {
        public int Id { get; set; }
        public int ConcertId { get; set; }
        public string UserId { get; set; }
        [Range(1, 5)]
        public int Rating { get; set; }
        public string Description { get; set; }
        public float? SentimentScore { get; set; }
        public DateTimeOffset CreatedTime { get; set; }
    }
}