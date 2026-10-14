using RikiPath.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RikiPath.Infrastructure.Configuration
{
    public class KanjiConfig : IEntityTypeConfiguration<Kanji>
    {
        public void Configure(EntityTypeBuilder<Kanji> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Character).IsRequired().HasMaxLength(10);
            builder.Property(x => x.Meaning).IsRequired().HasMaxLength(300);

            // Index trên Character để tìm kiếm Kanji nhanh
            builder.HasIndex(x => x.Character);

            builder.HasData(
                new Kanji
                {
                    Id = 910001,
                    Character = "日",
                    Meaning = "Mặt trời; ngày",
                    SinoVietnamese = "Nhật",
                    OnYomi = "ニチ、ジツ",
                    KunYomi = "ひ、か",
                    StrokeCount = 4,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910002,
                    Character = "月",
                    Meaning = "Mặt trăng; tháng",
                    SinoVietnamese = "Nguyệt",
                    OnYomi = "ゲツ、ガツ",
                    KunYomi = "つき",
                    StrokeCount = 4,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910003,
                    Character = "火",
                    Meaning = "Lửa",
                    SinoVietnamese = "Hỏa",
                    OnYomi = "カ",
                    KunYomi = "ひ、ほ",
                    StrokeCount = 4,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910004,
                    Character = "水",
                    Meaning = "Nước",
                    SinoVietnamese = "Thủy",
                    OnYomi = "スイ",
                    KunYomi = "みず",
                    StrokeCount = 4,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910005,
                    Character = "木",
                    Meaning = "Cây; gỗ",
                    SinoVietnamese = "Mộc",
                    OnYomi = "モク、ボク",
                    KunYomi = "き、こ",
                    StrokeCount = 4,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910006,
                    Character = "金",
                    Meaning = "Vàng; tiền",
                    SinoVietnamese = "Kim",
                    OnYomi = "キン、コン",
                    KunYomi = "かね、かな",
                    StrokeCount = 8,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910007,
                    Character = "土",
                    Meaning = "Đất",
                    SinoVietnamese = "Thổ",
                    OnYomi = "ド、ト",
                    KunYomi = "つち",
                    StrokeCount = 3,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910008,
                    Character = "山",
                    Meaning = "Núi",
                    SinoVietnamese = "Sơn",
                    OnYomi = "サン",
                    KunYomi = "やま",
                    StrokeCount = 3,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910009,
                    Character = "川",
                    Meaning = "Sông",
                    SinoVietnamese = "Xuyên",
                    OnYomi = "セン",
                    KunYomi = "かわ",
                    StrokeCount = 3,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                },
                new Kanji
                {
                    Id = 910010,
                    Character = "人",
                    Meaning = "Người",
                    SinoVietnamese = "Nhân",
                    OnYomi = "ジン、ニン",
                    KunYomi = "ひと",
                    StrokeCount = 2,
                    CreatedDate = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc),
                    IsDeleted = false
                });
        }
    }
}