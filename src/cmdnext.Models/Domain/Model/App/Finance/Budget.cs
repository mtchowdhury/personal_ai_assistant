using System;
using System.ComponentModel.DataAnnotations.Schema;
using CmdNext.Models.Domain.DTOs.Constants;
using CmdNext.Models.Domain.Model.Abstraction;

namespace CmdNext.Models.Domain.Model.App.Finance
{
    /// <summary>
    /// A per-category monthly budget. Absence of a budget must never break the UI.
    /// </summary>
    [Table("Budgets", Schema = DBSchema.Finance)]
    public class Budget : BaseEntity<Guid>
    {
        public Guid UserId { get; set; }

        public Guid CategoryId { get; set; }

        public Category? Category { get; set; }

        /// <summary>Monthly limit amount.</summary>
        public decimal MonthlyLimit { get; set; }

        public string Currency { get; set; } = "EUR";
    }
}
