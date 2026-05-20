using FluentValidation;
using Mets.Replenishment.Core.DTOs;

namespace Mets.Replenishment.Core.Validators;

public class ApproveRequestDtoValidator : AbstractValidator<ApproveRequestDto>
{
    public ApproveRequestDtoValidator()
    {
        RuleFor(x => x.ReviewerName)
            .NotEmpty().WithMessage("Reviewer name is required.")
            .MaximumLength(100).WithMessage("Reviewer name cannot exceed 100 characters.");
    }
}
