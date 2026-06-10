import { Calendar as CalendarIcon } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { format } from 'date-fns';
import { enGB } from 'date-fns/locale';
import {
  formatDateDisplay,
  formatDateTyping,
  parseDateToIso,
} from '@/lib/dates';
import { cn } from '@/lib/utils';
import { Calendar } from './ui/calendar';
import { Popover, PopoverContent, PopoverTrigger } from './ui/popover';

interface DateInputProps {
  value?: string;
  /** Called with yyyy-mm-dd, or '' to clear. Only fired on blur or calendar pick. */
  onChange: (isoValue: string) => void;
  className?: string;
}

function valueToDate(value?: string): Date | undefined {
  const iso = parseDateToIso(value ?? '');
  if (!iso) return undefined;
  const [y, m, d] = iso.split('-').map(Number);
  return new Date(y, m - 1, d);
}

/**
 * dd/mm/yyyy text field + calendar popup.
 * Saves only on blur or calendar selection — never mid-typing (fixes year-edit snap-back).
 */
export function DateInput({
  value,
  onChange,
  className = 'px-2 py-1 border border-border rounded text-sm bg-input-background w-[7.5rem]',
}: DateInputProps) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState(() => formatDateDisplay(value));
  const focused = useRef(false);
  const lastCommitted = useRef(parseDateToIso(value ?? '') ?? '');

  useEffect(() => {
    const propIso = parseDateToIso(value ?? '') ?? '';
    lastCommitted.current = propIso;
    if (!focused.current) {
      setDraft(formatDateDisplay(value));
    }
  }, [value]);

  const commitDraft = (raw: string) => {
    const trimmed = raw.trim();
    if (!trimmed) {
      if (lastCommitted.current === '') return;
      lastCommitted.current = '';
      onChange('');
      setDraft('');
      return;
    }

    const iso = parseDateToIso(trimmed);
    if (!iso) {
      setDraft(formatDateDisplay(value));
      return;
    }

    if (iso === lastCommitted.current) return;
    lastCommitted.current = iso;
    setDraft(formatDateDisplay(iso));
    onChange(iso);
  };

  const selected = valueToDate(value);

  return (
    <div className="inline-flex items-center gap-1">
      <input
        type="text"
        inputMode="numeric"
        placeholder="dd/mm/yyyy"
        value={draft}
        onFocus={() => {
          focused.current = true;
        }}
        onChange={(e) => {
          const next = e.target.value;
          if (/^\d*$/.test(next.replace(/\//g, ''))) {
            setDraft(formatDateTyping(next));
          } else {
            setDraft(next);
          }
        }}
        onBlur={(e) => {
          focused.current = false;
          commitDraft(e.target.value);
        }}
        onKeyDown={(e) => {
          if (e.key === 'Enter') {
            e.currentTarget.blur();
          }
        }}
        className={className}
      />
      <Popover open={open} onOpenChange={setOpen}>
        <PopoverTrigger asChild>
          <button
            type="button"
            className="p-1.5 border border-border rounded hover:bg-muted text-muted-foreground"
            title="Pick date"
            aria-label="Open calendar"
          >
            <CalendarIcon className="w-4 h-4" />
          </button>
        </PopoverTrigger>
        <PopoverContent className="w-auto p-0" align="start">
          <Calendar
            mode="single"
            locale={enGB}
            weekStartsOn={1}
            selected={selected}
            defaultMonth={selected}
            onSelect={(date) => {
              if (!date) return;
              const iso = format(date, 'yyyy-MM-dd');
              lastCommitted.current = iso;
              setDraft(formatDateDisplay(iso));
              onChange(iso);
              setOpen(false);
            }}
            className={cn('p-3')}
          />
        </PopoverContent>
      </Popover>
    </div>
  );
}
