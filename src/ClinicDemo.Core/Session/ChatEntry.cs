using ClinicDemo.Core.Models;

namespace ClinicDemo.Core.Session;

/// <summary>
/// One chat bubble for the UI. A visitor bubble has <see cref="Template"/> = null. An assistant bubble carries its
/// TemplateId, and a Summary bubble also carries the ReviewId it was shown for, so the UI can render Edit / Submit
/// only on the bubble whose ReviewId is the active review (I-5). Held in memory only and never logged.
/// </summary>
public sealed record ChatEntry(bool FromVisitor, string Text, TemplateId? Template = null, Guid? ReviewId = null);
