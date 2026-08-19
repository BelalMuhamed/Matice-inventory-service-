using System.Threading.Tasks;
using invetoryBackGroundServices.Common;
using invetoryBackGroundServices.Resources.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Localization;

namespace invetoryBackGroundServices.Filters
{
    /// <summary>
    /// Centralized localization of error responses. Runs after every action, reads
    /// <see cref="ApiError.Code"/> off the envelope, and replaces the message with its culture
    /// text (culture resolved from <c>Accept-Language</c> by the request-localization
    /// middleware). When no resource entry exists for the code, the English default already in
    /// <see cref="ApiError.Message"/> is left untouched.
    /// <para>
    /// Deliberately identical in mechanism to the Inventory API's filter of the same name -
    /// same interface, same fallback behavior - so error handling behaves the same way across
    /// both services rather than requiring the frontend to special-case one of them.
    /// </para>
    /// </summary>
    public sealed class LocalizeErrorResultFilter : IAsyncResultFilter
    {
        private readonly IStringLocalizer<Messages> _localizer;

        /// <summary>Creates the filter with the shared message localizer.</summary>
        public LocalizeErrorResultFilter(IStringLocalizer<Messages> localizer) => _localizer = localizer;

        /// <inheritdoc />
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            if (context.Result is ObjectResult { Value: ILocalizableApiResponse envelope } &&
                envelope.Error is { Code.Length: > 0 } error)
            {
                LocalizedString localized = error.MessageArg is null
                    ? _localizer[error.Code]
                    : _localizer[error.Code, error.MessageArg];

                if (!localized.ResourceNotFound)
                {
                    envelope.ReplaceErrorMessage(localized.Value);
                }
            }

            await next();
        }
    }
}
