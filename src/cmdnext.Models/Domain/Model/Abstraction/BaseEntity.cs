using System;
using System.ComponentModel.DataAnnotations;

namespace CmdNext.Models.Domain.Model.Abstraction
{
    public abstract class BaseEntity<TKey> where TKey : struct
    {
        [Key]
        public TKey Id { get; set; }

        public DateTime? CreatedOn { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedOn { get; set; }

        public Guid? CreatedBy { get; set; }

        public Guid? UpdatedBy { get; set; }
    }

    public abstract class BaseEntity : BaseEntity<int>
    {
    }
}
