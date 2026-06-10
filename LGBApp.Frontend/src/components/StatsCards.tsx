import { Users, DollarSign, TrendingUp, Target, FileText } from 'lucide-react';
import { useEffect, useState } from 'react';
import { ApiError, getDashboardStats, type DashboardStatsResponse } from '@/lib/api';

interface StatCardProps {
  title: string;
  value: string;
  change: string;
  icon: React.ReactNode;
  trend: 'up' | 'down';
}

function formatChange(change?: string): string {
  return change ?? '+0%';
}

function StatCard({ title, value, change, icon }: StatCardProps) {
  const changeText = formatChange(change);
  const isUp = changeText.startsWith('+') || changeText === '+0%';
  return (
    <div className="bg-card rounded-lg border border-border p-6">
      <div className="flex items-center justify-between mb-4">
        <span className="text-sm text-muted-foreground">{title}</span>
        <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center text-primary">
          {icon}
        </div>
      </div>
      <div className="mb-2">
        <div className="text-3xl font-semibold">{value}</div>
      </div>
      <div className={`text-sm flex items-center gap-1 ${isUp ? 'text-green-600' : 'text-red-600'}`}>
        <TrendingUp className={`w-4 h-4 ${!isUp ? 'rotate-180' : ''}`} />
        <span>{changeText} from last month</span>
      </div>
    </div>
  );
}

function AdHocServicesCard({ stats }: { stats: DashboardStatsResponse }) {
  const adHocRevenueChange = formatChange(stats.adHocRevenueChange);
  const isUp = adHocRevenueChange.startsWith('+');
  return (
    <div className="bg-card rounded-lg border border-border p-6">
      <div className="flex items-center justify-between mb-4">
        <span className="text-sm text-muted-foreground">Ad-hoc Services</span>
        <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center text-primary">
          <FileText className="w-5 h-5" />
        </div>
      </div>
      <div className="space-y-3">
        <div>
          <div className="text-sm text-muted-foreground mb-1">No. of Services</div>
          <div className="text-2xl font-semibold">{(stats.adHocServicesCount ?? 0).toLocaleString()}</div>
        </div>
        <div className="border-t border-border pt-3">
          <div className="text-sm text-muted-foreground mb-1">Revenue</div>
          <div className="text-2xl font-semibold">
            MYR {(stats.adHocRevenue ?? 0).toLocaleString('en-MY', { minimumFractionDigits: 0 })}
          </div>
        </div>
      </div>
      <div className={`text-sm flex items-center gap-1 mt-3 ${isUp ? 'text-green-600' : 'text-red-600'}`}>
        <TrendingUp className={`w-4 h-4 ${!isUp ? 'rotate-180' : ''}`} />
        <span>{adHocRevenueChange} from last month</span>
      </div>
    </div>
  );
}

interface StatsCardsProps {
  refreshKey?: number;
}

export function StatsCards({ refreshKey = 0 }: StatsCardsProps) {
  const [stats, setStats] = useState<DashboardStatsResponse | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!stats) setLoading(true);
    setError('');
    getDashboardStats()
      .then(setStats)
      .catch((err) => {
        setStats(null);
        setError(err instanceof ApiError ? err.message : 'Failed to load dashboard stats.');
      })
      .finally(() => setLoading(false));
  }, [refreshKey]);

  if (error) {
    return (
      <div className="rounded-lg border border-border bg-card p-6 text-center">
        <p className="text-sm text-muted-foreground mb-1">Dashboard stats unavailable</p>
        <p className="text-sm text-destructive">{error}</p>
      </div>
    );
  }

  if (loading) {
    return (
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
        {[1, 2, 3, 4, 5].map((i) => (
          <div key={i} className="bg-card rounded-lg border border-border p-6 h-36 animate-pulse" />
        ))}
      </div>
    );
  }

  if (!stats) {
    return null;
  }

  return (
    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
      <StatCard
        title="Active Customers"
        value={(stats.activeCustomers ?? 0).toLocaleString()}
        change={stats.activeCustomersChange}
        trend={formatChange(stats.activeCustomersChange).startsWith('-') ? 'down' : 'up'}
        icon={<Users className="w-5 h-5" />}
      />
      <StatCard
        title="Total Revenue"
        value={`MYR ${(stats.totalRevenue ?? 0).toLocaleString('en-MY', { minimumFractionDigits: 0 })}`}
        change={stats.totalRevenueChange}
        trend={formatChange(stats.totalRevenueChange).startsWith('-') ? 'down' : 'up'}
        icon={<DollarSign className="w-5 h-5" />}
      />
      <StatCard
        title="Outstanding Services"
        value={(stats.outstandingServices ?? 0).toLocaleString()}
        change={stats.outstandingServicesChange}
        trend={formatChange(stats.outstandingServicesChange).startsWith('-') ? 'down' : 'up'}
        icon={<Target className="w-5 h-5" />}
      />
      <StatCard
        title="Total Service Completed"
        value={(stats.totalServicesCompleted ?? 0).toLocaleString()}
        change={stats.totalServicesCompletedChange}
        trend={formatChange(stats.totalServicesCompletedChange).startsWith('-') ? 'down' : 'up'}
        icon={<TrendingUp className="w-5 h-5" />}
      />
      <AdHocServicesCard stats={stats} />
    </div>
  );
}
