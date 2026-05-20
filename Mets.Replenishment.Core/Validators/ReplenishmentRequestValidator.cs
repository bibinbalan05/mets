using FluentValidation;
using Mets.Replenishment.Core.Entities;

namespace Mets.Replenishment.Core.Validators;

public class ReplenishmentRequestValidator : AbstractValidator<ReplenishmentRequest>
{
    public ReplenishmentRequestValidator()
    {
        RuleFor(x => x.Location)
            .NotEmpty().WithMessage("Location is required.")
            .MaximumLength(100).WithMessage("Location cannot exceed 100 characters.");

        RuleFor(x => x.CreatedBy)
            .NotEmpty().WithMessage("Created By (Worker Name) is required.")
            .MaximumLength(100).WithMessage("Worker name cannot exceed 100 characters.");

        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("At least one replenishment item must be requested.")
            .Must(items => items != null && items.Count > 0).WithMessage("At least one replenishment item must be requested.");

        RuleForEach(x => x.Items)
            .SetValidator(new ReplenishmentRequestItemValidator());
    }
}
