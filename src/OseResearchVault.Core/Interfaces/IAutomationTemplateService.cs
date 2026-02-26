using OseResearchVault.Core.Models;

namespace OseResearchVault.Core.Interfaces;

public interface IAutomationTemplateService
{
    IReadOnlyList<AutomationTemplateRecord> GetTemplates();
    AutomationRecord CreateAutomationFromTemplate(string templateId);
}
