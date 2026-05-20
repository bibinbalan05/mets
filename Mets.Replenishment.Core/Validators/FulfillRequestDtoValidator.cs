using FluentValidation;
using Mets.Replenishment.Core.DTOs;

namespace Mets.Replenishment.Core.Validators;

public class FulfillRequestDtoValidator : AbstractValidator<FulfillRequestDto>
{
    public FulfillRequestDtoValidator()
    {
        RuleFor(x => x.FulfilledQuantities)
            .NotEmpty().WithMessage("At least one item must be fulfilled.")
            .Must(x => x != null && x.Any()).WithMessage("At least one item must be fulfilled.");

        RuleForEach(x => x.FulfilledQuantities)
            .Must(kvp => kvp.Value > 0).WithMessage("Fulfilled quantity must be greater than 0.");
    }
}
