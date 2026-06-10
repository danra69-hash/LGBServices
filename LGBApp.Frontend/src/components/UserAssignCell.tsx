import { X } from 'lucide-react';
import type { JobRequestUnitDto, UnitAssigneeDto } from '@/lib/api';

interface UserAssignCellProps {
  unit: JobRequestUnitDto;
  users: { id: number; name: string }[];
  disabled?: boolean;
  onAdd: (userId: number) => void;
  onRemove: (userId: number) => void;
}

export function UserAssignCell({ unit, users, disabled, onAdd, onRemove }: UserAssignCellProps) {
  const assignees: UnitAssigneeDto[] = unit.assignees?.length
    ? unit.assignees
    : unit.assignedUserId
      ? [{ userId: unit.assignedUserId, userName: unit.assignedUserName }]
      : [];

  const assignedIds = new Set(assignees.map((a) => a.userId));
  const available = users.filter((u) => !assignedIds.has(u.id));

  return (
    <div className="flex flex-wrap items-center gap-1 min-w-[10rem]">
      {assignees.map((a) => (
        <span
          key={a.userId}
          className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs bg-primary/10 text-primary border border-primary/20"
        >
          {a.userName}
          {!disabled && unit.status !== 'Completed' && (
            <button
              type="button"
              onClick={() => onRemove(a.userId)}
              className="hover:text-destructive"
              title={`Remove ${a.userName}`}
            >
              <X className="w-3 h-3" />
            </button>
          )}
        </span>
      ))}
      {!disabled && unit.status !== 'Completed' && (
        <select
          className="px-2 py-1 border border-border rounded text-xs bg-input-background max-w-[120px]"
          value=""
          onChange={(e) => {
            const userId = Number(e.target.value);
            if (userId) onAdd(userId);
          }}
        >
          <option value="">{assignees.length ? '+ Add user' : 'Assign user...'}</option>
          {available.map((u) => (
            <option key={u.id} value={u.id}>{u.name}</option>
          ))}
        </select>
      )}
      {assignees.length === 0 && disabled && <span className="text-muted-foreground">—</span>}
    </div>
  );
}
