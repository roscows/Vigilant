interface MetricCardProps {
  label: string;
  value: string | number;
  tone?: 'mint' | 'blue' | 'gold' | 'coral';
}

export function MetricCard({ label, value, tone = 'mint' }: MetricCardProps) {
  return (
    <article className={`glass-panel metric-card metric-card--${tone}`}>
      <span>{label}</span>
      <strong>{value}</strong>
    </article>
  );
}

