import { X, Calendar, User, MessageSquare, ClipboardList, Mail, Phone } from 'lucide-react';
import { useEffect, useState } from 'react';
import { DateInput } from './DateInput';
import { UserAssignCell } from './UserAssignCell';
import { formatDateDisplay } from '@/lib/dates';
import type { JobRequestResponse, JobRequestUnitDto } from '@/lib/api';

type JobRequest = JobRequestResponse;

interface JobRequestDetailsModalProps {
  isOpen: boolean;
  onClose: () => void;
  jobRequest: JobRequest | null;
  onAssign: (jobId: number, userId: number, acceptedDate: string, comments: string, unitNumber?: number, remove?: boolean) => void;
  users: { id: number; name: string }[];
  canAssign?: boolean;
}

export function JobRequestDetailsModal({
  isOpen,
  onClose,
  jobRequest,
  onAssign,
  users,
  canAssign = true,
}: JobRequestDetailsModalProps) {
  const [unitNumber, setUnitNumber] = useState('1');
  const [acceptedDate, setAcceptedDate] = useState('');
  const [comments, setComments] = useState('');

  useEffect(() => {
    if (!isOpen || !jobRequest) return;
    setUnitNumber(String(jobRequest.units?.[0]?.unitNumber ?? 1));
    setAcceptedDate('');
    setComments(jobRequest.assignmentComments ?? '');
  }, [isOpen, jobRequest]);

  const handleClose = () => {
    setUnitNumber('1');
    setAcceptedDate('');
    setComments('');
    onClose();
  };

  if (!isOpen || !jobRequest) return null;

  const multiUnit = jobRequest.totalQty > 1;
  const units = jobRequest.units?.length
    ? jobRequest.units
    : [{ unitNumber: 1, assignedUserName: jobRequest.jobAssignedTo, status: jobRequest.status } as JobRequestUnitDto];
  const selectedUnit = units.find((u) => u.unitNumber === Number(unitNumber)) ?? units[0];
  const editable = canAssign && jobRequest.status !== 'Completed' && jobRequest.status !== 'Canceled';

  const getStatusColor = (status: string) => {
    switch (status) {
      case 'Pending': return 'bg-yellow-100 text-yellow-800';
      case 'In Progress': return 'bg-blue-100 text-blue-800';
      case 'Completed': return 'bg-green-100 text-green-800';
      case 'Canceled': return 'bg-red-100 text-red-800';
      default: return 'bg-gray-100 text-gray-800';
    }
  };

  const taskLabel = jobRequest.taskType || jobRequest.service;
  const isFormTask = ['MOI', 'MOI Approval', 'MOA'].includes(jobRequest.taskType);

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-50 p-4">
      <div className="bg-card rounded-lg border border-border w-full max-w-3xl max-h-[90vh] overflow-hidden flex flex-col">
        <div className="p-6 border-b border-border flex items-center justify-between">
          <h2>Job Request Details</h2>
          <button
            onClick={handleClose}
            className="p-1 hover:bg-muted rounded transition-colors"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto">
          <div className="p-6 space-y-6">
            {isFormTask && (
              <div className="bg-primary/5 border border-primary/20 rounded-lg p-4 space-y-2">
                <p className="text-sm font-medium text-primary">External signer — send {taskLabel} to:</p>
                <p className="text-lg font-semibold">{jobRequest.accountHolder || '—'}</p>
                <p className="text-sm text-muted-foreground">
                  Client company: {jobRequest.customer}
                </p>
                <div className="flex flex-wrap gap-4 text-sm pt-1">
                  {jobRequest.accountHolderEmail && (
                    <span className="flex items-center gap-1.5">
                      <Mail className="w-4 h-4 text-muted-foreground" />
                      {jobRequest.accountHolderEmail}
                    </span>
                  )}
                  {jobRequest.accountHolderPhone && (
                    <span className="flex items-center gap-1.5">
                      <Phone className="w-4 h-4 text-muted-foreground" />
                      {jobRequest.accountHolderPhone}
                    </span>
                  )}
                </div>
              </div>
            )}

            <div className="bg-muted/30 rounded-lg p-4 space-y-3">
              <h3 className="mb-3">Job Information</h3>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <span className="text-sm text-muted-foreground">Customer</span>
                  <p className="font-medium">{jobRequest.customer}</p>
                </div>
                <div>
                  <span className="text-sm text-muted-foreground">Task</span>
                  <p className="font-medium flex items-center gap-2">
                    <ClipboardList className="w-4 h-4 text-muted-foreground" />
                    {taskLabel}
                  </p>
                </div>
                <div>
                  <span className="text-sm text-muted-foreground">Usage</span>
                  <p className="font-medium">{jobRequest.usedQty}/{jobRequest.totalQty}</p>
                </div>
                <div>
                  <span className="text-sm text-muted-foreground">Date Requested</span>
                  <p className="font-medium">{formatDateDisplay(jobRequest.dateRequested)}</p>
                </div>
                {!isFormTask && (
                  <div>
                    <span className="text-sm text-muted-foreground">Send To</span>
                    <p className="font-medium">{jobRequest.accountHolder || '—'}</p>
                  </div>
                )}
                <div>
                  <span className="text-sm text-muted-foreground">Users</span>
                  <p className="font-medium">{jobRequest.jobAssignedTo || '—'}</p>
                </div>
                <div>
                  <span className="text-sm text-muted-foreground">Current Status</span>
                  <p>
                    <span className={`px-2 py-1 rounded-full text-xs ${getStatusColor(jobRequest.status)}`}>
                      {jobRequest.status}
                    </span>
                  </p>
                </div>
              </div>
            </div>

            {editable && (
              <div className="space-y-4 border border-border rounded-lg p-4">
                <h3 className="mb-1">Manage users for {taskLabel}</h3>
                <p className="text-xs text-muted-foreground mb-3">
                  Add or remove users anytime. Changes sync to each user&apos;s work tracker.
                </p>

                {multiUnit && (
                  <div>
                    <label className="flex items-center gap-2 mb-2">
                      <span>Unit #</span>
                    </label>
                    <select
                      value={unitNumber}
                      onChange={(e) => setUnitNumber(e.target.value)}
                      className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                    >
                      {units.map((u) => (
                        <option key={u.unitNumber} value={u.unitNumber}>
                          Unit {u.unitNumber} ({u.status})
                        </option>
                      ))}
                    </select>
                  </div>
                )}

                <div>
                  <label className="flex items-center gap-2 mb-2">
                    <User className="w-4 h-4 text-muted-foreground" />
                    <span>Assigned users</span>
                  </label>
                  {selectedUnit && (
                    <UserAssignCell
                      unit={selectedUnit}
                      users={users}
                      onAdd={(userId) => onAssign(jobRequest.id, userId, acceptedDate, comments, selectedUnit.unitNumber)}
                      onRemove={(userId) => onAssign(jobRequest.id, userId, acceptedDate, comments, selectedUnit.unitNumber, true)}
                    />
                  )}
                </div>

                <div>
                  <label className="flex items-center gap-2 mb-2">
                    <Calendar className="w-4 h-4 text-muted-foreground" />
                    <span>Accepted date (optional)</span>
                  </label>
                  <DateInput
                    value={acceptedDate}
                    onChange={setAcceptedDate}
                    className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring"
                  />
                </div>

                <div>
                  <label className="flex items-center gap-2 mb-2">
                    <MessageSquare className="w-4 h-4 text-muted-foreground" />
                    <span>Comments</span>
                  </label>
                  <textarea
                    rows={3}
                    value={comments}
                    onChange={(e) => setComments(e.target.value)}
                    className="w-full px-3 py-2 border border-border rounded-lg bg-input-background focus:outline-none focus:ring-2 focus:ring-ring resize-none"
                    placeholder="Notes for the user..."
                  />
                </div>
              </div>
            )}

            {!editable && (
              <div className="bg-muted/30 rounded-lg p-4">
                <p className="text-sm text-muted-foreground">
                  {jobRequest.status === 'Pending' && !canAssign
                    ? 'Waiting for admin to assign a user.'
                    : `This job is ${jobRequest.status.toLowerCase()}.`}
                  {jobRequest.jobAssignedTo && ` Users: ${jobRequest.jobAssignedTo}`}
                </p>
              </div>
            )}
          </div>
        </div>

        <div className="p-4 border-t border-border flex justify-end">
          <button
            type="button"
            onClick={handleClose}
            className="px-6 py-2 border border-border rounded-lg hover:bg-muted transition-colors"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  );
}
