import { useState } from 'react'
import {
  CategoryScale,
  Chart as ChartJS,
  Legend,
  LinearScale,
  LineElement,
  PointElement,
  Title,
  Tooltip,
} from 'chart.js'
import { Line } from 'react-chartjs-2'
import { API_BASE_URL } from './api'

ChartJS.register(CategoryScale, LinearScale, PointElement, LineElement, Title, Tooltip, Legend)

const METRIC_OPTIONS = [
  { key: 'rounds', label: 'Rounds Played', color: '#206a42' },
  { key: 'cartRentals', label: 'Cart Rentals', color: '#4f7f5f' },
  { key: 'totalRevenue', label: 'Total Revenue', color: '#2563eb' },
  { key: 'proShopRevenue', label: 'Pro Shop Revenue', color: '#7c3aed' },
  { key: 'foodAndBeverageRevenue', label: 'Food & Beverage Revenue', color: '#c45a1c' },
  { key: 'alcoholRevenue', label: 'Alcohol Revenue', color: '#9a3412' },
  { key: 'rangeBallRevenue', label: 'Range Ball Revenue', color: '#0f766e' },
  { key: 'highTemp', label: 'High Temp', color: '#dc2626' },
  { key: 'lowTemp', label: 'Low Temp', color: '#0284c7' },
  { key: 'rainfall', label: 'Rainfall', color: '#1d4ed8' },
  { key: 'gdd', label: 'GDD', color: '#ca8a04' },
  { key: 'averageMoisture', label: 'Average Moisture', color: '#16a34a' },
  { key: 'openMaintenanceTasks', label: 'Open Maintenance Tasks', color: '#64748b' },
  { key: 'completedMaintenanceTasks', label: 'Completed Maintenance Tasks', color: '#15803d' },
  { key: 'equipmentIssues', label: 'Equipment Issues', color: '#b91c1c' },
]

const DEFAULT_METRICS = ['rounds', 'totalRevenue', 'highTemp']

function getTodayDate() {
  const today = new Date()
  const year = today.getFullYear()
  const month = String(today.getMonth() + 1).padStart(2, '0')
  const day = String(today.getDate()).padStart(2, '0')

  return `${year}-${month}-${day}`
}

function addDays(value, days) {
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)
  date.setDate(date.getDate() + days)

  return date.toISOString().slice(0, 10)
}

function formatChartDate(value) {
  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)

  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
  }).format(date)
}

function formatNumber(value) {
  if (value === null || value === undefined) {
    return 'No data'
  }

  return Number(value).toLocaleString('en-US', {
    maximumFractionDigits: 2,
  })
}

function getMetricOption(metricKey) {
  return METRIC_OPTIONS.find((metric) => metric.key === metricKey)
}

function getMetricValues(points, metricKey) {
  return points
    .map((point) => point.values?.[metricKey] ?? point.Values?.[metricKey])
    .filter((value) => value !== null && value !== undefined)
    .map(Number)
}

function buildSummary(points, metricKey) {
  const values = getMetricValues(points, metricKey)

  if (values.length === 0) {
    return {
      average: null,
      min: null,
      max: null,
      latest: null,
    }
  }

  return {
    average: values.reduce((sum, value) => sum + value, 0) / values.length,
    min: Math.min(...values),
    max: Math.max(...values),
    latest: values.at(-1),
  }
}

function buildRelationshipSummary(points, selectedMetrics) {
  const summaries = []

  if (selectedMetrics.includes('foodAndBeverageRevenue') && selectedMetrics.includes('highTemp')) {
    const warmDays = points.filter((point) => (point.values?.highTemp ?? point.Values?.highTemp ?? 0) >= 80)
    const coolerDays = points.filter((point) => {
      const highTemp = point.values?.highTemp ?? point.Values?.highTemp
      return highTemp !== null && highTemp !== undefined && highTemp < 80
    })
    const warmAverage = averageMetric(warmDays, 'foodAndBeverageRevenue')
    const coolerAverage = averageMetric(coolerDays, 'foodAndBeverageRevenue')

    if (warmAverage !== null && coolerAverage !== null && warmAverage > coolerAverage) {
      summaries.push('F&B revenue increased on warmer days in this range.')
    }
  }

  if (selectedMetrics.includes('rounds') && selectedMetrics.includes('rainfall')) {
    const rainyDays = points.filter((point) => (point.values?.rainfall ?? point.Values?.rainfall ?? 0) > 0)
    const dryDays = points.filter((point) => (point.values?.rainfall ?? point.Values?.rainfall ?? 0) === 0)
    const rainyAverage = averageMetric(rainyDays, 'rounds')
    const dryAverage = averageMetric(dryDays, 'rounds')

    if (rainyAverage !== null && dryAverage !== null && rainyAverage < dryAverage) {
      summaries.push('Rounds decreased on days with rainfall.')
    }
  }

  if (selectedMetrics.includes('averageMoisture') && selectedMetrics.includes('rainfall')) {
    const rainyDays = points.filter((point) => (point.values?.rainfall ?? point.Values?.rainfall ?? 0) > 0)
    const dryDays = points.filter((point) => (point.values?.rainfall ?? point.Values?.rainfall ?? 0) === 0)
    const rainyMoisture = averageMetric(rainyDays, 'averageMoisture')
    const dryMoisture = averageMetric(dryDays, 'averageMoisture')

    if (rainyMoisture !== null && dryMoisture !== null && rainyMoisture > dryMoisture) {
      summaries.push('Average moisture was higher on days with rainfall.')
    }
  }

  return summaries.length === 0
    ? ['No clear relationship summary yet for the selected metrics and range.']
    : summaries
}

function averageMetric(points, metricKey) {
  const values = getMetricValues(points, metricKey)

  if (values.length === 0) {
    return null
  }

  return values.reduce((sum, value) => sum + value, 0) / values.length
}

function AnalyticsLab() {
  const today = getTodayDate()
  const [startDate, setStartDate] = useState(addDays(today, -6))
  const [endDate, setEndDate] = useState(today)
  const [selectedMetrics, setSelectedMetrics] = useState(DEFAULT_METRICS)
  const [analyticsData, setAnalyticsData] = useState([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState('')

  async function loadAnalytics(event) {
    event.preventDefault()

    if (selectedMetrics.length === 0) {
      setError('Choose at least one metric to compare.')
      return
    }

    setLoading(true)
    setError('')

    try {
      const metricQuery = selectedMetrics.join(',')
      const response = await fetch(
        `${API_BASE_URL}/api/analytics/compare?startDate=${startDate}&endDate=${endDate}&metrics=${metricQuery}`,
      )

      if (!response.ok) {
        throw new Error('Analytics request failed.')
      }

      setAnalyticsData(await response.json())
    } catch {
      setError('Could not load analytics data. Please check the dates and try again.')
    } finally {
      setLoading(false)
    }
  }

  function toggleMetric(metricKey) {
    setSelectedMetrics((currentMetrics) =>
      currentMetrics.includes(metricKey)
        ? currentMetrics.filter((metric) => metric !== metricKey)
        : [...currentMetrics, metricKey],
    )
  }

  const chartData = {
    labels: analyticsData.map((point) => formatChartDate(point.date || point.Date)),
    datasets: selectedMetrics.map((metricKey) => {
      const metric = getMetricOption(metricKey)

      return {
        label: metric?.label || metricKey,
        data: analyticsData.map((point) => point.values?.[metricKey] ?? point.Values?.[metricKey] ?? null),
        borderColor: metric?.color || '#206a42',
        backgroundColor: metric?.color || '#206a42',
        tension: 0.35,
      }
    }),
  }
  const chartOptions = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        display: true,
      },
    },
    scales: {
      y: {
        beginAtZero: true,
      },
    },
  }
  const relationshipSummary = buildRelationshipSummary(analyticsData, selectedMetrics)

  return (
    <main className="dashboard-page">
      <header className="dashboard-header">
        <div className="header-copy">
          <p className="eyebrow">Analytics Lab</p>
          <h1>Compare Course Variables</h1>
        </div>
      </header>

      <form className="analytics-controls" onSubmit={loadAnalytics}>
        <div className="form-row">
          <div className="form-field">
            <label htmlFor="analytics-start-date">Start Date</label>
            <input
              id="analytics-start-date"
              type="date"
              value={startDate}
              onChange={(event) => setStartDate(event.target.value)}
            />
          </div>

          <div className="form-field">
            <label htmlFor="analytics-end-date">End Date</label>
            <input
              id="analytics-end-date"
              type="date"
              value={endDate}
              onChange={(event) => setEndDate(event.target.value)}
            />
          </div>
        </div>

        <div className="metric-picker">
          {METRIC_OPTIONS.map((metric) => (
            <label key={metric.key}>
              <input
                type="checkbox"
                checked={selectedMetrics.includes(metric.key)}
                onChange={() => toggleMetric(metric.key)}
              />
              <span>{metric.label}</span>
            </label>
          ))}
        </div>

        <button type="submit" disabled={loading}>
          {loading ? 'Loading...' : 'Compare Metrics'}
        </button>
      </form>

      {error && <p className="status-message error">{error}</p>}

      <section className="dashboard-section">
        <h2>Comparison Chart</h2>
        <div className="analytics-chart-card">
          {analyticsData.length === 0 ? (
            <p className="empty-message">Choose metrics and compare a date range.</p>
          ) : (
            <Line data={chartData} options={chartOptions} />
          )}
        </div>
      </section>

      {analyticsData.length > 0 && (
        <>
          <section className="dashboard-section">
            <h2>Relationship Summary</h2>
            <div className="relationship-list">
              {relationshipSummary.map((summary) => (
                <p key={summary}>{summary}</p>
              ))}
            </div>
          </section>

          <section className="dashboard-section">
            <h2>Metric Summary</h2>
            <div className="analytics-summary-grid">
              {selectedMetrics.map((metricKey) => {
                const metric = getMetricOption(metricKey)
                const summary = buildSummary(analyticsData, metricKey)

                return (
                  <article className="analytics-summary-card" key={metricKey}>
                    <span>{metric?.label || metricKey}</span>
                    <strong>{formatNumber(summary.average)}</strong>
                    <p>Avg</p>
                    <small>
                      Min {formatNumber(summary.min)} · Max {formatNumber(summary.max)} · Latest {formatNumber(summary.latest)}
                    </small>
                  </article>
                )
              })}
            </div>
          </section>

          <section className="dashboard-section">
            <h2>Comparison Table</h2>
            <div className="analytics-table-wrap">
              <table className="analytics-table">
                <thead>
                  <tr>
                    <th>Date</th>
                    {selectedMetrics.map((metricKey) => (
                      <th key={metricKey}>{getMetricOption(metricKey)?.label || metricKey}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {analyticsData.map((point) => (
                    <tr key={point.date || point.Date}>
                      <td>{point.date || point.Date}</td>
                      {selectedMetrics.map((metricKey) => (
                        <td key={`${point.date || point.Date}-${metricKey}`}>
                          {formatNumber(point.values?.[metricKey] ?? point.Values?.[metricKey])}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        </>
      )}
    </main>
  )
}

export default AnalyticsLab
