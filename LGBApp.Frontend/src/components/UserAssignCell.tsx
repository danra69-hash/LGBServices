import { Plus, UserPlus, Users, X } from 'lucide-react';
import { useMemo, useState } from 'react';
import { Popover, PopoverContent, PopoverTrigger } from './ui/popover';
import type { JobRequestUnitDto, UnitAssigneeDto } from '@/lib/api';
import { cn } from '@/lib/utils';

interface AssignableUser {
  id: number;
  name: string;
}

interface UserAssignCellProps {
  unit: JobRequestUnitDto;
  users: AssignableUser[];
  /** Internal sec team for bulk tag (excludes admin). Shown when `onAddMany` is set. */
  secTeamUsers?: AssignableUser[];
  disabled?: boolean;
  onAdd: (userId: number) => void;
  onRemove: (userId: number) => void;
  onAddMany?: (userIds: number[]) => void | Promise<void>;
}

const AVATAR_COLORS = [
  'bg-violet-500',
  'bg-teal-600',
  'bg-sky-600',
  'bg-amber-600',
  'bg-rose-500',
  'bg-indigo-500',
] as const;

function userInitials(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

function avatarColor(userId: number): string {
  return AVATAR_COLORS[userId % AVATAR_COLORS.length];
}

function MemberAvatar({ userId, name, size = 'sm' }: { userId: number; name: string; size?: 'sm' | 'md' }) {
  const dim = size === 'md' ? 'h-7 w-7 text-[11px]' : 'h-6 w-6 text-[10px]';
  return (
    <span
      className={cn(
        'inline-flex shrink-0 items-center justify-center rounded-full font-semibold text-white shadow-sm',
        dim,
        avatarColor(userId),
      )}
      aria-hidden
    >
      {userInitials(name)}
    </span>
  );
}

function MemberChip({
  userId,
  name,
  onRemove,
  canRemove,
}: {
  userId: number;
  name: string;
  onRemove: () => void;
  canRemove: boolean;
}) {
  return (
    <span className="inline-flex max-w-[11rem] items-center gap-1.5 rounded-full border border-slate-200/90 bg-gradient-to-b from-white to-slate-50 py-0.5 pl-0.5 pr-1.5 text-xs font-medium text-slate-800 shadow-sm ring-1 ring-black/[0.03]">
      <MemberAvatar userId={userId} name={name} />
      <span className="truncate">{name}</span>
      {canRemove && (
        <button
          type="button"
          onClick={onRemove}
          className="ml-0.5 rounded-full p-0.5 text-slate-400 transition-colors hover:bg-rose-50 hover:text-rose-600"
          title={`Remove ${name}`}
        >
          <X className="h-3 w-3" />
        </button>
      )}
    </span>
  );
}

export function UserAssignCell({
  unit,
  users,
  secTeamUsers,
  disabled,
  onAdd,
  onRemove,
  onAddMany,
}: UserAssignCellProps) {
  const [open, setOpen] = useState(false);
  const [bulkLoading, setBulkLoading] = useState(false);

  const assignees: UnitAssigneeDto[] = unit.assignees?.length
    ? unit.assignees
    : unit.assignedUserId
      ? [{ userId: unit.assignedUserId, userName: unit.assignedUserName }]
      : [];

  const assignedIds = useMemo(() => new Set(assignees.map((a) => a.userId)), [assignees]);
  const available = users.filter((u) => !assignedIds.has(u.id));
  const secBulkPool = (secTeamUsers ?? []).filter((u) => !assignedIds.has(u.id));
  const canEdit = !disabled && unit.status !== 'Completed';
  const showBulkTag = Boolean(onAddMany && secBulkPool.length > 0);

  const handleAdd = (userId: number) => {
    onAdd(userId);
    if (available.length <= 1) setOpen(false);
  };

  const handleBulkTag = async () => {
    if (!onAddMany || secBulkPool.length === 0) return;
    setBulkLoading(true);
    try {
      await onAddMany(secBulkPool.map((u) => u.id));
      setOpen(false);
    } finally {
      setBulkLoading(false);
    }
  };

  return (
    <div className="flex min-w-[12rem] flex-wrap items-center gap-1.5">
      {assignees.map((a) => (
        <MemberChip
          key={a.userId}
          userId={a.userId}
          name={a.userName}
          canRemove={canEdit}
          onRemove={() => onRemove(a.userId)}
        />
      ))}

      {canEdit && (available.length > 0 || showBulkTag) && (
        <Popover open={open} onOpenChange={setOpen}>
          <PopoverTrigger asChild>
            <button
              type="button"
              className="inline-flex h-8 w-8 items-center justify-center rounded-full border-2 border-dashed border-slate-300 bg-white text-slate-500 shadow-sm transition-all hover:border-primary hover:bg-primary/5 hover:text-primary focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/30"
              title="Add team member"
            >
              <Plus className="h-4 w-4" />
            </button>
          </PopoverTrigger>
          <PopoverContent className="w-64 p-0" align="start">
            <div className="border-b border-border px-3 py-2.5">
              <p className="text-xs font-semibold text-foreground">Add team member</p>
              <p className="text-[11px] text-muted-foreground">Tag secretarial staff on this item</p>
            </div>

            {showBulkTag && (
              <div className="border-b border-border p-2">
                <button
                  type="button"
                  disabled={bulkLoading}
                  onClick={() => void handleBulkTag()}
                  className="flex w-full items-center gap-2 rounded-lg border border-sky-200 bg-gradient-to-r from-sky-50 to-indigo-50 px-3 py-2 text-left text-xs font-medium text-sky-900 transition-colors hover:from-sky-100 hover:to-indigo-100 disabled:opacity-60"
                >
                  <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-full bg-sky-600 text-white shadow-sm">
                    <Users className="h-3.5 w-3.5" />
                  </span>
                  <span className="min-w-0 flex-1">
                    <span className="block">Tag all sec team</span>
                    <span className="block font-normal text-sky-700/80">
                      {secBulkPool.length} member{secBulkPool.length === 1 ? '' : 's'} (excl. admin)
                    </span>
                  </span>
                </button>
              </div>
            )}

            {available.length > 0 ? (
              <ul className="max-h-52 overflow-y-auto p-1.5">
                {available.map((u) => (
                  <li key={u.id}>
                    <button
                      type="button"
                      onClick={() => handleAdd(u.id)}
                      className="flex w-full items-center gap-2.5 rounded-lg px-2 py-2 text-left text-sm transition-colors hover:bg-muted"
                    >
                      <MemberAvatar userId={u.id} name={u.name} size="md" />
                      <span className="min-w-0 flex-1 truncate font-medium text-foreground">{u.name}</span>
                      <UserPlus className="h-3.5 w-3.5 shrink-0 text-muted-foreground" />
                    </button>
                  </li>
                ))}
              </ul>
            ) : (
              <p className="px-3 py-3 text-xs text-muted-foreground">Everyone available is already tagged.</p>
            )}
          </PopoverContent>
        </Popover>
      )}

      {assignees.length === 0 && disabled && <span className="text-muted-foreground">—</span>}
    </div>
  );
}
