/** Series colors that stay apart for color-blind readers and read on both the light and the dark theme. */
export const SERIES_COLORS = ['#2563eb', '#d97706', '#059669', '#db2777', '#7c3aed', '#0891b2', '#65a30d', '#dc2626'] as const

export const seriesColor = (index: number) => SERIES_COLORS[index % SERIES_COLORS.length]!

export const AXIS_TICK = { fill: 'var(--muted-foreground)', fontSize: 12 }
export const GRID_STROKE = 'var(--border)'
export const TOOLTIP_STYLE = {
  background: 'var(--card)',
  border: '1px solid var(--border)',
  borderRadius: 8,
  color: 'var(--card-foreground)',
}

/** Recharts measures its parent; this size is used until it has (and in tests, where there is no layout). */
export const INITIAL_SIZE = { width: 640, height: 288 }
