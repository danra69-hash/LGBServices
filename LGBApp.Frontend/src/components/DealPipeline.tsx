import { TrendingUp, DollarSign } from 'lucide-react';

interface Deal {
  id: number;
  title: string;
  company: string;
  value: number;
  stage: 'lead' | 'qualified' | 'proposal' | 'negotiation' | 'closed';
  probability: number;
}

const mockDeals: Deal[] = [
  { id: 1, title: 'Enterprise License', company: 'Acme Corp', value: 50000, stage: 'negotiation', probability: 80 },
  { id: 2, title: 'Annual Subscription', company: 'TechStart Inc', value: 25000, stage: 'proposal', probability: 60 },
  { id: 3, title: 'Consulting Package', company: 'Global Systems', value: 35000, stage: 'qualified', probability: 40 },
  { id: 4, title: 'Implementation Services', company: 'DataFlow Ltd', value: 45000, stage: 'negotiation', probability: 75 },
  { id: 5, title: 'Training Program', company: 'Innovation Hub', value: 15000, stage: 'lead', probability: 20 },
];

export function DealPipeline() {
  const stages = ['lead', 'qualified', 'proposal', 'negotiation', 'closed'];
  const stageLabels = {
    lead: 'Lead',
    qualified: 'Qualified',
    proposal: 'Proposal',
    negotiation: 'Negotiation',
    closed: 'Closed Won'
  };

  const getDealsByStage = (stage: string) => {
    return mockDeals.filter(deal => deal.stage === stage);
  };

  const getTotalValueByStage = (stage: string) => {
    return getDealsByStage(stage).reduce((sum, deal) => sum + deal.value, 0);
  };

  return (
    <div className="bg-card rounded-lg border border-border overflow-hidden">
      <div className="p-4 border-b border-border">
        <div className="flex items-center gap-2">
          <TrendingUp className="w-5 h-5 text-muted-foreground" />
          <h2>Deal Pipeline</h2>
        </div>
      </div>

      <div className="p-4 overflow-x-auto">
        <div className="flex gap-4 min-w-max">
          {stages.map((stage) => {
            const deals = getDealsByStage(stage);
            const totalValue = getTotalValueByStage(stage);

            return (
              <div key={stage} className="flex-shrink-0 w-64">
                <div className="mb-3">
                  <div className="flex items-center justify-between mb-1">
                    <h4 className="text-sm">{stageLabels[stage as keyof typeof stageLabels]}</h4>
                    <span className="text-xs text-muted-foreground">{deals.length}</span>
                  </div>
                  <div className="flex items-center gap-1 text-sm text-muted-foreground">
                    <DollarSign className="w-3 h-3" />
                    {totalValue.toLocaleString()}
                  </div>
                </div>

                <div className="space-y-2">
                  {deals.map((deal) => (
                    <div
                      key={deal.id}
                      className="bg-muted/30 rounded-lg p-3 border border-border hover:border-primary/50 transition-colors cursor-pointer"
                    >
                      <div className="mb-2">
                        <div className="font-medium mb-1 text-sm">{deal.title}</div>
                        <div className="text-xs text-muted-foreground">{deal.company}</div>
                      </div>
                      <div className="flex items-center justify-between">
                        <div className="flex items-center gap-1">
                          <DollarSign className="w-3 h-3 text-muted-foreground" />
                          <span className="text-sm">{deal.value.toLocaleString()}</span>
                        </div>
                        <span className="text-xs text-muted-foreground">{deal.probability}%</span>
                      </div>
                    </div>
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
