import { useCallback, useEffect, useState } from 'react';
import { ApiError, getDivisionGroups, getWorkflowTemplates, updateDivisionGroup, updateWorkflowTemplate, type DivisionGroupDto, type WorkflowTemplateDto } from '@/lib/api';

interface AdminWorkflowConfigProps {
  refreshKey?: number;
}

export function AdminWorkflowConfig({ refreshKey = 0 }: AdminWorkflowConfigProps) {
  const [groups, setGroups] = useState<DivisionGroupDto[]>([]);
  const [templates, setTemplates] = useState<WorkflowTemplateDto[]>([]);
  const [selectedTemplate, setSelectedTemplate] = useState<WorkflowTemplateDto | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    setError('');
    try {
      const [g, t] = await Promise.all([getDivisionGroups(), getWorkflowTemplates('MOA')]);
      setGroups(g);
      setTemplates(t);
      setSelectedTemplate((prev) => prev ?? t[0] ?? null);
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to load workflow config.');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load, refreshKey]);

  const saveTemplate = async () => {
    if (!selectedTemplate) return;
    setSaving(true);
    setMessage('');
    try {
      await updateWorkflowTemplate(selectedTemplate.id, selectedTemplate);
      setMessage('Workflow template saved.');
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save template.');
    } finally {
      setSaving(false);
    }
  };

  const saveGroup = async (group: DivisionGroupDto) => {
    setSaving(true);
    setMessage('');
    try {
      await updateDivisionGroup(group.id, group);
      setMessage(`Division group "${group.name}" saved.`);
      await load();
    } catch (err) {
      setError(err instanceof ApiError ? err.message : 'Failed to save division group.');
    } finally {
      setSaving(false);
    }
  };

  if (loading) return <p className="text-sm text-muted-foreground p-4">Loading workflow config…</p>;

  return (
    <div className="bg-card border border-border rounded-lg p-6 space-y-6">
      <div>
        <h3 className="text-lg font-medium">MOA Workflow Templates</h3>
        <p className="text-sm text-muted-foreground mt-1">
          Conditional approval chains (No LOA / With LOA / SWM). Edit steps, conditions, and assignees.
        </p>
      </div>

      {error && <p className="text-sm text-destructive">{error}</p>}
      {message && <p className="text-sm text-green-600">{message}</p>}

      <div className="flex flex-wrap gap-2">
        {templates.map((t) => (
          <button
            key={t.id}
            type="button"
            onClick={() => setSelectedTemplate(t)}
            className={`px-3 py-1.5 rounded-lg text-sm border ${
              selectedTemplate?.id === t.id ? 'bg-primary text-primary-foreground border-primary' : 'border-border'
            }`}
          >
            {t.name}
          </button>
        ))}
      </div>

      {selectedTemplate && (
        <div className="space-y-3">
          <p className="text-sm text-muted-foreground">{selectedTemplate.description}</p>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b border-border text-left">
                  <th className="p-2">#</th>
                  <th className="p-2">Step</th>
                  <th className="p-2">Condition</th>
                  <th className="p-2">Assignee type</th>
                  <th className="p-2">Role / Name</th>
                </tr>
              </thead>
              <tbody>
                {selectedTemplate.steps.map((step, idx) => (
                  <tr key={step.id || idx} className="border-b border-border/50">
                    <td className="p-2">{step.stepOrder}</td>
                    <td className="p-2">
                      <input
                        className="w-full px-2 py-1 border border-border rounded bg-input-background"
                        value={step.displayName}
                        onChange={(e) => {
                          const steps = [...selectedTemplate.steps];
                          steps[idx] = { ...step, displayName: e.target.value };
                          setSelectedTemplate({ ...selectedTemplate, steps });
                        }}
                      />
                    </td>
                    <td className="p-2">
                      <select
                        className="w-full px-2 py-1 border border-border rounded bg-input-background"
                        value={step.conditionType}
                        onChange={(e) => {
                          const steps = [...selectedTemplate.steps];
                          steps[idx] = { ...step, conditionType: e.target.value };
                          setSelectedTemplate({ ...selectedTemplate, steps });
                        }}
                      >
                        <option value="Always">Always</option>
                        <option value="FinanceRelated">Finance related</option>
                        <option value="BankSignatory">Bank signatory</option>
                        <option value="Applicable">If applicable</option>
                        <option value="LoaHolders">LOA holders</option>
                        <option value="BoardApproval">Board approval</option>
                      </select>
                    </td>
                    <td className="p-2">
                      <select
                        className="w-full px-2 py-1 border border-border rounded bg-input-background"
                        value={step.assigneeType}
                        onChange={(e) => {
                          const steps = [...selectedTemplate.steps];
                          steps[idx] = { ...step, assigneeType: e.target.value };
                          setSelectedTemplate({ ...selectedTemplate, steps });
                        }}
                      >
                        <option value="JobTitle">Job title</option>
                        <option value="NamedUser">Named user</option>
                        <option value="ProjectInitiator">Project initiator</option>
                        <option value="LoaHolders">LOA holders</option>
                        <option value="BoardMembers">Board members</option>
                        <option value="ExternalName">External name</option>
                      </select>
                    </td>
                    <td className="p-2">
                      <input
                        className="w-full px-2 py-1 border border-border rounded bg-input-background"
                        value={step.assigneeDisplayName || step.assigneeRole || ''}
                        onChange={(e) => {
                          const steps = [...selectedTemplate.steps];
                          steps[idx] = {
                            ...step,
                            assigneeDisplayName: e.target.value,
                            assigneeRole: step.assigneeType === 'JobTitle' ? e.target.value : step.assigneeRole,
                          };
                          setSelectedTemplate({ ...selectedTemplate, steps });
                        }}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <button
            type="button"
            disabled={saving}
            onClick={() => void saveTemplate()}
            className="px-4 py-2 bg-primary text-primary-foreground rounded-lg text-sm disabled:opacity-50"
          >
            Save workflow template
          </button>
        </div>
      )}

      <div className="border-t border-border pt-6">
        <h4 className="font-medium mb-3">Division groups &amp; recommenders</h4>
        <div className="space-y-4 max-h-64 overflow-y-auto">
          {groups.map((group) => (
            <div key={group.id} className="border border-border rounded-lg p-3 space-y-2">
              <div className="flex flex-wrap gap-2 items-center">
                <span className="font-medium">{group.name}</span>
                <span className="text-xs text-muted-foreground">({group.code})</span>
                <select
                  className="ml-auto text-sm px-2 py-1 border border-border rounded"
                  value={group.moaWorkflowTemplateCode}
                  onChange={(e) =>
                    setGroups(groups.map((g) => (g.id === group.id ? { ...g, moaWorkflowTemplateCode: e.target.value } : g)))
                  }
                >
                  {templates.map((t) => (
                    <option key={t.code} value={t.code}>{t.name}</option>
                  ))}
                </select>
                <button
                  type="button"
                  disabled={saving}
                  onClick={() => void saveGroup(group)}
                  className="text-sm px-2 py-1 border border-border rounded hover:bg-muted"
                >
                  Save
                </button>
              </div>
              <p className="text-xs text-muted-foreground">
                Recommenders: {group.recommenders.map((r) => r.displayName).join(', ') || 'None'}
              </p>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
