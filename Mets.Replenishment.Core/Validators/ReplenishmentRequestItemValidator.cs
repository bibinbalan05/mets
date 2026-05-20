using FluentValidation;
using Mets.Replenishment.Core.Entities;

namespace Mets.Replenishment.Core.Validators;

public class ReplenishmentRequestItemValidator : AbstractValidator<ReplenishmentRequestItem>
{
    public ReplenishmentRequestItemValidator()
    {
        RuleFor(x => x.ArticleNumber)
            .NotEmpty().WithMessage("Article number is required.")
            .Must(x => x != null && x.StartsWith("ART-")).WithMessage("Article number must start with 'ART-'.")
            .MaximumLength(50).WithMessage("Article number cannot exceed 50 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(200).WithMessage("Description cannot exceed 200 characters.");

        RuleFor(x => x.RequestedQuantity)
            .GreaterThan(0).WithMessage("Requested quantity must be greater than 0.");
    }
}
