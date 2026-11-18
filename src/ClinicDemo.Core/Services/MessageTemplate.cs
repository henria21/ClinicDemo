using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Services;

/// <summary>An approved Hebrew template (placeholders in braces) and its reviewed "what this asserts" note.</summary>
public sealed record MessageTemplate(TemplateId Id, string Text, string Asserts);
