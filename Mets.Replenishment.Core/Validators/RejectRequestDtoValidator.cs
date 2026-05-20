using FluentValidation;
using Mets.Replenishment.Core.DTOs;

namespace Mets.Replenishment.Core.Validators;

public class RejectRequestDtoValidator : AbstractValidator<RejectRequestDto>
{
    public RejectRequestDtoValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Rejection reason is required.")
            .MaximumLength(500).WithMessage("Rejection reason cannot exceed 500 characters.");

        RuleFor(x => x.ReviewerName)
            .NotEmpty().WithMessage("Reviewer name is required.")
            .MaximumLength(100).WithMessage("Reviewer name cannot exceed 100 characters.");
    }
}
