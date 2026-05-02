import { useEffect, useState } from 'react'
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

const MAINTENANCE_CATEGORIES = ['Greens', 'Fairways', 'Irrigation', 'Equipment', 'Clubhouse']
const PRIORITY_OPTIONS = ['Low', 'Medium', 'High', 'Critical']
const SEVERITY_OPTIONS = ['Low', 'Medium', 'High', 'Critical']

const EMPTY_MAINTENANCE_FORM = {
  title: '',
  description: '',
  category: 'Greens',
  priority: 'Medium',
}

const EMPTY_EQUIPMENT_FORM = {
  equipmentName: '',
  issueDescription: '',
  severity: 'Medium',
}

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

function formatDisplayDate(value) {
  if (!value) {
    return ''
  }

  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)

  return new Intl.DateTimeFormat('en-US', {
    month: 'long',
    day: 'numeric',
    year: 'numeric',
  }).format(date)
}

function formatShortDate(value) {
  if (!value) {
    return 'Not scheduled'
  }

  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)

  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
  }).format(date)
}

function formatChartDate(value) {
  if (!value) {
    return ''
  }

  const [year, month, day] = value.split('-').map(Number)
  const date = new Date(year, month - 1, day)

  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
  }).format(date)
}

function formatCurrency(value) {
  if (value === null || value === undefined) {
    return 'Not logged'
  }

  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency: 'USD',
  }).format(value)
}

function formatValue(value) {
  if (value === null || value === undefined || value === '') {
    return 'Not logged'
  }

  return value
}

function formatGdd(value) {
  if (value === null || value === undefined) {
    return 'Not logged'
  }

  return Number(value).toFixed(1)
}

function formatPercent(value) {
  if (value === null || value === undefined) {
    return 'Not logged'
  }

  return `${Number(value).toFixed(1)}%`
}

function formatMoisture(value) {
  if (value === null || value === undefined) {
    return 'Not logged'
  }

  return `${Number(value).toFixed(1)}%`
}

function StatCard({ label, value, alert }) {
  return (
    <div className={alert ? 'stat-card alert' : 'stat-card'}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

function DriestLocationsList({ locations }) {
  if (!locations || locations.length === 0) {
    return <p className="empty-message compact">No moisture readings logged for this date.</p>
  }

  return (
    <div className="driest-list">
      {locations.map((location, index) => (
        <div className="driest-row" key={`${location.location || location.Location}-${location.zone || location.Zone}-${index}`}>
          <div>
            <strong>{location.location || location.Location}</strong>
            <span>{location.zone || location.Zone || 'No zone logged'}</span>
          </div>
          <strong>{formatMoisture(location.value ?? location.Value)}</strong>
        </div>
      ))}
    </div>
  )
}

function getAlertSeverity(alert) {
  return (alert?.severity || alert?.Severity || 'Info').toLowerCase()
}

function AlertCard({ alert }) {
  const severity = getAlertSeverity(alert)
  const relatedItems = alert.relatedItems || alert.RelatedItems || []

  return (
    <article className={`action-card ${severity}`}>
      <div className="action-card-header">
        <span>{alert.category || alert.Category || 'Operations'}</span>
        <strong>{alert.severity || alert.Severity || 'Info'}</strong>
      </div>
      <h3>{cleanDisplayText(alert.title || alert.Title)}</h3>
      <p>{cleanDisplayText(alert.message || alert.Message)}</p>
      {relatedItems.length > 0 && (
        <ul className="related-items">
          {relatedItems.map((item, index) => (
            <li key={`${item}-${index}`}>{cleanDisplayText(item)}</li>
          ))}
        </ul>
      )}
      <div className="recommended-action">
        <span>Recommended Action</span>
        <p>{cleanDisplayText(alert.recommendedAction || alert.RecommendedAction)}</p>
      </div>
    </article>
  )
}

function PriorityList({ priorities }) {
  if (priorities.length === 0) {
    return <p className="empty-message">No priority actions for this date.</p>
  }

  return (
    <ol className="priority-list">
      {priorities.map((priority, index) => (
        <li key={`${priority.title || priority.Title}-${index}`}>
          <div>
            <strong>{cleanDisplayText(priority.title || priority.Title)}</strong>
            <p>{cleanDisplayText(priority.description || priority.Description)}</p>
          </div>
          <span>{priority.category || priority.Category}</span>
        </li>
      ))}
    </ol>
  )
}

function getItemValue(item, camelCaseKey, pascalCaseKey) {
  return item?.[camelCaseKey] ?? item?.[pascalCaseKey]
}

function getItemId(item) {
  return getItemValue(item, 'id', 'Id')
}

function getItemStatus(item) {
  return getItemValue(item, 'status', 'Status') || 'Open'
}

function getExternalSourceTag(item) {
  const isExternal = getItemValue(item, 'isExternal', 'IsExternal')
  const sourceName = getItemValue(item, 'externalSourceName', 'ExternalSourceName')

  if (!isExternal || !sourceName) {
    return null
  }

  return sourceName.includes('ASB') ? 'ASB' : cleanDisplayText(sourceName)
}

function cleanDisplayText(value) {
  if (value === null || value === undefined) {
    return value
  }

  return String(value)
    .replace(/\[Demo\]\s*/g, '')
    .replace(/\bmock\s+/gi, '')
    .replace(/\bplaceholder\s+/gi, '')
    .trim()
}

function normalizeStatus(status) {
  const value = String(status || 'Open').replace(/\s/g, '')

  if (value === 'Repaired') {
    return 'Resolved'
  }

  return value
}

function formatStatus(status) {
  const normalizedStatus = normalizeStatus(status)
  const labels = {
    Open: 'Open',
    InProgress: 'In Progress',
    WaitingOnParts: 'Waiting on Parts',
    Completed: 'Completed',
    Resolved: 'Resolved',
    Blocked: 'Blocked',
  }

  return labels[normalizedStatus] || status
}

function formatTime(value) {
  if (!value) {
    return null
  }

  return new Intl.DateTimeFormat('en-US', {
    hour: 'numeric',
    minute: '2-digit',
  }).format(new Date(value))
}

function TaskManagementList({ title, items, type, actionLoading, onAction }) {
  const isMaintenance = type === 'maintenance'
  const closedStatus = isMaintenance ? 'Completed' : 'Resolved'
  const activeItems = items.filter((item) => normalizeStatus(getItemStatus(item)) !== closedStatus)
  const emptyActiveMessage = isMaintenance ? 'No active maintenance tasks.' : 'No active equipment issues.'
  const groups = isMaintenance
    ? [
        { status: 'Open', label: 'Open' },
        { status: 'InProgress', label: 'In Progress' },
        { status: 'Blocked', label: 'Blocked' },
        { status: 'Completed', label: 'Completed', quiet: true },
      ]
    : [
        { status: 'Open', label: 'Open' },
        { status: 'InProgress', label: 'In Progress' },
        { status: 'WaitingOnParts', label: 'Waiting on Parts' },
        { status: 'Resolved', label: 'Resolved', quiet: true },
      ]

  if (items.length === 0) {
    return (
      <div className="task-list-card">
        <h3>{title}</h3>
        <p className="task-empty">{emptyActiveMessage}</p>
      </div>
    )
  }

  return (
    <div className="task-list-card">
      <h3>{title}</h3>
      {activeItems.length === 0 && <p className="task-empty compact">{emptyActiveMessage}</p>}
      <div className="task-group-list">
        {groups.map((group) => {
          const groupItems = items.filter((item) => normalizeStatus(getItemStatus(item)) === group.status)

          if (groupItems.length === 0) {
            return null
          }

          const groupContent = (
            <>
              <div className="task-group-heading">
                <span>{group.label}</span>
                <strong>{groupItems.length}</strong>
              </div>
              <div className="task-row-list">
                {groupItems.map((item) => (
                  <TaskManagementRow
                    item={item}
                    type={type}
                    isMaintenance={isMaintenance}
                    actionLoading={actionLoading}
                    onAction={onAction}
                    key={`${type}-${getItemId(item)}`}
                  />
                ))}
              </div>
            </>
          )

          if (group.quiet) {
            return (
              <details className="task-status-group quiet" key={`${type}-${group.status}`}>
                <summary className="task-group-heading">
                  <span>{group.label}</span>
                  <strong>{groupItems.length}</strong>
                </summary>
                <div className="task-row-list">
                  {groupItems.map((item) => (
                    <TaskManagementRow
                      item={item}
                      type={type}
                      isMaintenance={isMaintenance}
                      actionLoading={actionLoading}
                      onAction={onAction}
                      key={`${type}-${getItemId(item)}`}
                    />
                  ))}
                </div>
              </details>
            )
          }

          return (
            <div className="task-status-group" key={`${type}-${group.status}`}>
              {groupContent}
            </div>
          )
        })}
      </div>
    </div>
  )
}

function TaskManagementRow({ item, type, isMaintenance, actionLoading, onAction }) {
  const id = getItemId(item)
  const status = normalizeStatus(getItemStatus(item))
  const loadingStart = actionLoading === `${type}-${id}-start`
  const loadingFinish = actionLoading === `${type}-${id}-finish`
  const loadingWaiting = actionLoading === `${type}-${id}-waiting-on-parts`
  const titleText = isMaintenance
    ? getItemValue(item, 'title', 'Title')
    : getItemValue(item, 'equipmentName', 'EquipmentName')
  const detailText = isMaintenance
    ? getItemValue(item, 'category', 'Category')
    : getItemValue(item, 'issueDescription', 'IssueDescription')
  const urgencyText = isMaintenance
    ? getItemValue(item, 'priority', 'Priority')
    : getItemValue(item, 'severity', 'Severity')
  const startedAt = formatTime(getItemValue(item, 'startedAt', 'StartedAt'))
  const completedAt = formatTime(getItemValue(item, 'completedAt', 'CompletedAt'))
  const sourceTag = getExternalSourceTag(item)
  const severityClass = !isMaintenance && urgencyText === 'Critical' ? ' critical' : ''

  return (
    <article className={`task-row status-${status.toLowerCase()}${severityClass}`}>
      <div className="task-row-header">
        <div>
          <div className="task-title-line">
            <strong>{cleanDisplayText(titleText)}</strong>
            {sourceTag && <span className="source-tag">{sourceTag}</span>}
          </div>
          <span>{cleanDisplayText(detailText)}</span>
        </div>
        <span className={`status-pill status-${status.toLowerCase()}`}>{formatStatus(status)}</span>
      </div>

      <div className="task-row-meta">
        <span className={urgencyText === 'Critical' ? 'severity-tag critical' : 'severity-tag'}>{urgencyText}</span>
        {startedAt && <small>Started at {startedAt}</small>}
        {completedAt && <small>Completed at {completedAt}</small>}
      </div>

      <div className="task-row-actions">
        {status === 'Open' && (
          <button
            type="button"
            className="secondary"
            disabled={Boolean(actionLoading)}
            onClick={() => onAction(type, id, 'start')}
          >
            {loadingStart ? 'Starting...' : 'Start'}
          </button>
        )}

        {isMaintenance && status === 'InProgress' && (
          <button
            type="button"
            disabled={Boolean(actionLoading)}
            onClick={() => onAction(type, id, 'complete')}
          >
            {loadingFinish ? 'Completing...' : 'Complete'}
          </button>
        )}

        {!isMaintenance && status === 'InProgress' && (
          <button
            type="button"
            className="secondary warning"
            disabled={Boolean(actionLoading)}
            onClick={() => onAction(type, id, 'waiting-on-parts')}
          >
            {loadingWaiting ? 'Updating...' : 'Waiting on Parts'}
          </button>
        )}

        {!isMaintenance && (status === 'InProgress' || status === 'WaitingOnParts') && (
          <button
            type="button"
            disabled={Boolean(actionLoading)}
            onClick={() => onAction(type, id, 'resolve')}
          >
            {loadingFinish ? 'Resolving...' : 'Resolve'}
          </button>
        )}
      </div>
    </article>
  )
}

function CreateTaskIssueSection({
  maintenanceForm,
  equipmentForm,
  createLoading,
  createMessage,
  createError,
  onMaintenanceChange,
  onEquipmentChange,
  onCreateMaintenance,
  onCreateEquipment,
}) {
  return (
    <section className="dashboard-section">
      <h2>Create Task / Issue</h2>
      <div className="create-entry-grid">
        <form className="entry-form-card" onSubmit={onCreateMaintenance}>
          <h3>Maintenance Task</h3>
          <div className="form-field">
            <label htmlFor="task-title">Title</label>
            <input
              id="task-title"
              type="text"
              value={maintenanceForm.title}
              onChange={(event) => onMaintenanceChange('title', event.target.value)}
              required
            />
          </div>

          <div className="form-field">
            <label htmlFor="task-description">Description</label>
            <textarea
              id="task-description"
              value={maintenanceForm.description}
              onChange={(event) => onMaintenanceChange('description', event.target.value)}
              rows="3"
            />
          </div>

          <div className="form-row">
            <div className="form-field">
              <label htmlFor="task-category">Category</label>
              <select
                id="task-category"
                value={maintenanceForm.category}
                onChange={(event) => onMaintenanceChange('category', event.target.value)}
              >
                {MAINTENANCE_CATEGORIES.map((category) => (
                  <option key={category} value={category}>
                    {category}
                  </option>
                ))}
              </select>
            </div>

            <div className="form-field">
              <label htmlFor="task-priority">Priority</label>
              <select
                id="task-priority"
                value={maintenanceForm.priority}
                onChange={(event) => onMaintenanceChange('priority', event.target.value)}
              >
                {PRIORITY_OPTIONS.map((priority) => (
                  <option key={priority} value={priority}>
                    {priority}
                  </option>
                ))}
              </select>
            </div>
          </div>

          <button type="submit" disabled={createLoading === 'maintenance'}>
            {createLoading === 'maintenance' ? 'Creating...' : 'Create Task'}
          </button>
        </form>

        <form className="entry-form-card" onSubmit={onCreateEquipment}>
          <h3>Equipment Issue</h3>
          <div className="form-field">
            <label htmlFor="equipment-name">Equipment Name</label>
            <input
              id="equipment-name"
              type="text"
              value={equipmentForm.equipmentName}
              onChange={(event) => onEquipmentChange('equipmentName', event.target.value)}
              required
            />
          </div>

          <div className="form-field">
            <label htmlFor="issue-description">Issue Description</label>
            <textarea
              id="issue-description"
              value={equipmentForm.issueDescription}
              onChange={(event) => onEquipmentChange('issueDescription', event.target.value)}
              rows="3"
            />
          </div>

          <div className="form-field">
            <label htmlFor="issue-severity">Severity</label>
            <select
              id="issue-severity"
              value={equipmentForm.severity}
              onChange={(event) => onEquipmentChange('severity', event.target.value)}
            >
              {SEVERITY_OPTIONS.map((severity) => (
                <option key={severity} value={severity}>
                  {severity}
                </option>
              ))}
            </select>
          </div>

          <button type="submit" disabled={createLoading === 'equipment'}>
            {createLoading === 'equipment' ? 'Creating...' : 'Create Issue'}
          </button>
        </form>
      </div>

      {createMessage && <p className="import-message success">{createMessage}</p>}
      {createError && <p className="import-message error">{createError}</p>}
    </section>
  )
}

function DashboardSection({ title, children }) {
  return (
    <section className="dashboard-section">
      <h2>{title}</h2>
      <div className="section-grid">{children}</div>
    </section>
  )
}

function BriefingBox({ title, text, variant = 'default' }) {
  if (!text) {
    return null
  }

  return (
    <section className={`daily-briefing ${variant}`}>
      <p>{title}</p>
      <strong>{cleanDisplayText(text)}</strong>
    </section>
  )
}

function DemoModeControl({ status, loading, message, error, onLoadDemo, onClearDemo }) {
  const playCount = status?.demoPlayRecordCount ?? 0
  const salesCount = status?.demoSalesRecordCount ?? 0
  const weatherCount = status?.demoWeatherRecordCount ?? 0
  const hasDemoData = status?.demoDataExists

  return (
    <section className="demo-mode">
      <div>
        {hasDemoData && <span className="demo-active-badge">Demo Mode Active</span>}
        <small>
          {hasDemoData
            ? `${playCount} play, ${salesCount} sales, ${weatherCount} weather records loaded`
            : 'Optional presentation data'}
        </small>
      </div>
      <div className="demo-actions">
        <button type="button" onClick={onLoadDemo} disabled={loading}>
          {loading ? 'Working...' : 'Load Sample Data'}
        </button>
        <button type="button" className="secondary" onClick={onClearDemo} disabled={loading || !hasDemoData}>
          Clear Sample Data
        </button>
      </div>
      {message && <p className="demo-message success">{message}</p>}
      {error && <p className="demo-message error">{error}</p>}
    </section>
  )
}

function ImportControl({ label, buttonText, file, loading, result, error, onFileChange, onImport }) {
  return (
    <div className="import-control">
      <label>{label}</label>
      <input type="file" accept=".csv,text/csv" onChange={onFileChange} />
      <button type="button" onClick={onImport} disabled={loading || !file}>
        {loading ? 'Importing...' : buttonText}
      </button>
      {result && <p className="import-message success">{result}</p>}
      {error && <p className="import-message error">{error}</p>}
    </div>
  )
}

function TrendChart({ title, label, points, color }) {
  const chartData = {
    labels: points.map((point) => formatChartDate(point.date)),
    datasets: [
      {
        label,
        data: points.map((point) => point.value),
        borderColor: color,
        backgroundColor: color,
        tension: 0.35,
      },
    ],
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

  return (
    <div className="trend-card">
      <h3>{title}</h3>
      <div className="trend-chart">
        {points.length === 0 ? (
          <p className="empty-message">No trend data available.</p>
        ) : (
          <Line data={chartData} options={chartOptions} />
        )}
      </div>
    </div>
  )
}

function ForecastCard({ forecast, loading, error }) {
  if (loading) {
    return (
      <section className="forecast-card">
        <div>
          <span>Forecast</span>
          <h2>Tomorrow's Forecast</h2>
        </div>
        <p className="empty-message compact">Loading forecast...</p>
      </section>
    )
  }

  if (error) {
    return (
      <section className="forecast-card">
        <div>
          <span>Forecast</span>
          <h2>Tomorrow's Forecast</h2>
        </div>
        <p className="status-message error">{error}</p>
      </section>
    )
  }

  if (!forecast) {
    return null
  }

  return (
    <section className="forecast-card">
      <div className="forecast-heading">
        <div>
          <span>Forecast</span>
          <h2>Tomorrow's Forecast</h2>
        </div>
        <strong>{forecast.confidenceLevel || 'Low'} Confidence</strong>
      </div>

      <div className="forecast-metrics">
        <StatCard label="Expected Rounds" value={forecast.predictedRounds ?? 0} />
        <StatCard
          label="Expected Revenue"
          value={formatCurrency(forecast.predictedTotalRevenue ?? forecast.predictedRevenue)}
        />
        <StatCard
          label="Expected F&B"
          value={formatCurrency(forecast.predictedFoodAndBeverageRevenue)}
        />
        <StatCard label="Expected Carts" value={forecast.predictedCartRentals ?? 0} />
        <StatCard label="Expected GDD" value={formatGdd(forecast.predictedGdd)} />
      </div>

      <p>{cleanDisplayText(forecast.explanation || forecast.summary)}</p>
    </section>
  )
}

function AdminPayrollSection({
  payrollSummary,
  reminders,
  loading,
  error,
  actionLoading,
  onSubmitPayroll,
  onCompleteReminder,
}) {
  const period = payrollSummary?.period || payrollSummary?.Period
  const daysUntilDue = payrollSummary?.daysUntilDue ?? payrollSummary?.DaysUntilDue
  const isOverdue = payrollSummary?.isOverdue ?? payrollSummary?.IsOverdue
  const isDueToday = payrollSummary?.isDueToday ?? payrollSummary?.IsDueToday
  const status = period?.status || period?.Status || 'Open'
  const periodId = period?.id || period?.Id
  const activeReminders = reminders
    .filter((reminder) => !(reminder.isCompleted ?? reminder.IsCompleted))
    .slice(0, 5)

  function getDueText() {
    if (status !== 'Open') {
      return status
    }

    if (isOverdue) {
      return `${Math.abs(daysUntilDue)} day(s) overdue`
    }

    if (isDueToday) {
      return 'Due today'
    }

    return `${daysUntilDue ?? 0} day(s) remaining`
  }

  return (
    <section className="dashboard-section">
      <h2>Admin & Payroll</h2>
      {loading && <p className="status-message">Loading admin deadlines...</p>}
      {error && <p className="status-message error">{error}</p>}

      {!loading && !error && (
        <div className="admin-payroll-grid">
          <article className="admin-card payroll-card">
            <div className="admin-card-header">
              <span>Current Payroll Period</span>
              <strong className={`status-pill status-${status.toLowerCase()}`}>{status}</strong>
            </div>
            <h3>
              {formatShortDate(period?.periodStartDate || period?.PeriodStartDate)} -{' '}
              {formatShortDate(period?.periodEndDate || period?.PeriodEndDate)}
            </h3>
            <div className="admin-meta-grid">
              <div>
                <span>Payroll Due</span>
                <strong>{formatShortDate(period?.payrollDueDate || period?.PayrollDueDate)}</strong>
              </div>
              <div className={isOverdue || isDueToday ? 'admin-deadline urgent' : 'admin-deadline'}>
                <span>Deadline</span>
                <strong>{getDueText()}</strong>
              </div>
            </div>
            <button
              type="button"
              disabled={!periodId || status !== 'Open' || Boolean(actionLoading)}
              onClick={() => onSubmitPayroll(periodId)}
            >
              {actionLoading === 'payroll-submit' ? 'Submitting...' : 'Mark Payroll Submitted'}
            </button>
          </article>

          <article className="admin-card">
            <div className="admin-card-header">
              <span>Upcoming Reminders</span>
              <strong>{activeReminders.length}</strong>
            </div>
            {activeReminders.length === 0 ? (
              <p className="empty-message compact">No upcoming admin reminders.</p>
            ) : (
              <div className="admin-reminder-list">
                {activeReminders.map((reminder) => {
                  const reminderId = reminder.id || reminder.Id

                  return (
                    <div className="admin-reminder-row" key={reminderId}>
                      <div>
                        <strong>{cleanDisplayText(reminder.title || reminder.Title)}</strong>
                        <span>
                          {reminder.category || reminder.Category} - Due{' '}
                          {formatShortDate(reminder.dueDate || reminder.DueDate)}
                        </span>
                      </div>
                      <button
                        type="button"
                        disabled={Boolean(actionLoading)}
                        onClick={() => onCompleteReminder(reminderId)}
                      >
                        {actionLoading === `reminder-${reminderId}` ? 'Completing...' : 'Complete'}
                      </button>
                    </div>
                  )
                })}
              </div>
            )}
          </article>
        </div>
      )}
    </section>
  )
}

function getInsightMessage(insight) {
  if (typeof insight === 'string') {
    return insight
  }

  return insight?.message || insight?.Message || 'No insight message available.'
}

function getInsightSeverity(message) {
  const text = message.toLowerCase()

  if (text.includes('critical')) {
    return 'critical'
  }

  if (text.includes('warning')) {
    return 'warning'
  }

  return 'info'
}

function Dashboard() {
  const [selectedDate, setSelectedDate] = useState(getTodayDate())
  const [dashboard, setDashboard] = useState(null)
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(false)
  const [playCsvFile, setPlayCsvFile] = useState(null)
  const [salesCsvFile, setSalesCsvFile] = useState(null)
  const [playImportLoading, setPlayImportLoading] = useState(false)
  const [salesImportLoading, setSalesImportLoading] = useState(false)
  const [playImportResult, setPlayImportResult] = useState('')
  const [salesImportResult, setSalesImportResult] = useState('')
  const [playImportError, setPlayImportError] = useState('')
  const [salesImportError, setSalesImportError] = useState('')
  const [gddStartDate, setGddStartDate] = useState(getTodayDate())
  const [gddEndDate, setGddEndDate] = useState(getTodayDate())
  const [gddRangeResult, setGddRangeResult] = useState(null)
  const [gddRangeLoading, setGddRangeLoading] = useState(false)
  const [gddRangeError, setGddRangeError] = useState('')
  const [trends, setTrends] = useState({
    rounds: [],
    revenue: [],
    gdd: [],
  })
  const [trendsLoading, setTrendsLoading] = useState(false)
  const [trendsError, setTrendsError] = useState('')
  const [forecast, setForecast] = useState(null)
  const [forecastLoading, setForecastLoading] = useState(false)
  const [forecastError, setForecastError] = useState('')
  const [payrollSummary, setPayrollSummary] = useState(null)
  const [adminReminders, setAdminReminders] = useState([])
  const [adminLoading, setAdminLoading] = useState(false)
  const [adminError, setAdminError] = useState('')
  const [adminActionLoading, setAdminActionLoading] = useState('')
  const [demoStatus, setDemoStatus] = useState(null)
  const [demoLoading, setDemoLoading] = useState(false)
  const [demoMessage, setDemoMessage] = useState('')
  const [demoError, setDemoError] = useState('')
  const [maintenanceTasks, setMaintenanceTasks] = useState([])
  const [equipmentIssues, setEquipmentIssues] = useState([])
  const [taskManagementLoading, setTaskManagementLoading] = useState(false)
  const [taskManagementError, setTaskManagementError] = useState('')
  const [taskActionLoading, setTaskActionLoading] = useState('')
  const [maintenanceForm, setMaintenanceForm] = useState(EMPTY_MAINTENANCE_FORM)
  const [equipmentForm, setEquipmentForm] = useState(EMPTY_EQUIPMENT_FORM)
  const [createLoading, setCreateLoading] = useState('')
  const [createMessage, setCreateMessage] = useState('')
  const [createError, setCreateError] = useState('')

  useEffect(() => {
    if (!selectedDate) {
      setDashboard(null)
      setTrends({ rounds: [], revenue: [], gdd: [] })
      setForecast(null)
      setPayrollSummary(null)
      setAdminReminders([])
      setError('')
      setTrendsError('')
      setForecastError('')
      setAdminError('')
      return
    }

    setGddStartDate(addDays(selectedDate, -29))
    setGddEndDate(selectedDate)
    setGddRangeResult(null)
    setGddRangeError('')
    loadDashboardData(selectedDate)
  }, [selectedDate])

  useEffect(() => {
    loadDemoStatus()
  }, [])

  function handleGoToToday() {
    setSelectedDate(getTodayDate())
  }

  async function loadDashboardData(dateToLoad) {
    await Promise.all([
      loadDashboard(dateToLoad),
      loadTrends(dateToLoad),
      loadForecast(addDays(dateToLoad, 1)),
      loadAdminData(dateToLoad),
      loadTaskManagementData(),
    ])
  }

  async function loadDashboard(dateToLoad = selectedDate) {
    setLoading(true)
    setError('')
    setDashboard(null)

    try {
      const response = await fetch(`${API_BASE_URL}/api/dashboard/${dateToLoad}`)

      if (!response.ok) {
        setError(`Dashboard request failed with status ${response.status}. Check the API error details, then refresh the dashboard.`)
        return
      }

      const text = await response.text()

      if (!text) {
        setError('No dashboard data was returned.')
        return
      }

      setDashboard(JSON.parse(text))
    } catch {
      setError(`Could not reach the Course Commander API at ${API_BASE_URL}. Start the ASP.NET Core API, then refresh the dashboard.`)
    } finally {
      setLoading(false)
    }
  }

  async function loadDemoStatus() {
    try {
      const response = await fetch(`${API_BASE_URL}/api/demo/status`)

      if (!response.ok) {
        throw new Error('Demo status request failed.')
      }

      setDemoStatus(await response.json())
    } catch {
      setDemoError('Could not load sample data status.')
    }
  }

  async function updateDemoData(endpoint, method, successMessage) {
    setDemoLoading(true)
    setDemoMessage('')
    setDemoError('')

    try {
      const response = await fetch(`${API_BASE_URL}${endpoint}`, { method })

      if (!response.ok) {
        throw new Error('Sample data request failed.')
      }

      setDemoStatus(await response.json())
      setDemoMessage(successMessage)
      await loadDashboardData(selectedDate)
    } catch {
      setDemoError('Sample data update failed. Please make sure the API is running and try again.')
    } finally {
      setDemoLoading(false)
    }
  }

  async function loadTaskManagementData() {
    setTaskManagementLoading(true)
    setTaskManagementError('')

    try {
      const [maintenanceResponse, equipmentResponse] = await Promise.all([
        fetch(`${API_BASE_URL}/api/maintenance-tasks`),
        fetch(`${API_BASE_URL}/api/equipment-issues`),
      ])

      if (!maintenanceResponse.ok || !equipmentResponse.ok) {
        throw new Error('Task management request failed.')
      }

      const [tasks, issues] = await Promise.all([
        maintenanceResponse.json(),
        equipmentResponse.json(),
      ])

      setMaintenanceTasks(tasks)
      setEquipmentIssues(issues)
    } catch {
      setTaskManagementError('Could not load maintenance tasks or equipment issues.')
    } finally {
      setTaskManagementLoading(false)
    }
  }

  async function updateTaskStatus(type, id, action) {
    const endpoint =
      type === 'maintenance'
        ? `/api/maintenance-tasks/${id}/${action}`
        : `/api/equipment-issues/${id}/${action}`

    const loadingAction = action === 'start' ? 'start' : action === 'waiting-on-parts' ? 'waiting-on-parts' : 'finish'
    setTaskActionLoading(`${type}-${id}-${loadingAction}`)
    setTaskManagementError('')

    try {
      const response = await fetch(`${API_BASE_URL}${endpoint}`, { method: 'PUT' })

      if (!response.ok) {
        throw new Error('Status update failed.')
      }

      await loadDashboardData(selectedDate)
    } catch {
      setTaskManagementError('Could not update the item status. Please try again.')
    } finally {
      setTaskActionLoading('')
    }
  }

  function updateMaintenanceForm(field, value) {
    setMaintenanceForm((currentForm) => ({
      ...currentForm,
      [field]: value,
    }))
  }

  function updateEquipmentForm(field, value) {
    setEquipmentForm((currentForm) => ({
      ...currentForm,
      [field]: value,
    }))
  }

  async function createMaintenanceTask(event) {
    event.preventDefault()
    setCreateLoading('maintenance')
    setCreateMessage('')
    setCreateError('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/maintenance-tasks`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          ...maintenanceForm,
          status: 'Open',
        }),
      })

      if (!response.ok) {
        throw new Error('Maintenance task request failed.')
      }

      setMaintenanceForm(EMPTY_MAINTENANCE_FORM)
      setCreateMessage('Maintenance task created.')
      await loadDashboardData(selectedDate)
    } catch {
      setCreateError('Could not create the maintenance task. Please check the required fields and try again.')
    } finally {
      setCreateLoading('')
    }
  }

  async function createEquipmentIssue(event) {
    event.preventDefault()
    setCreateLoading('equipment')
    setCreateMessage('')
    setCreateError('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/equipment-issues`, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({
          ...equipmentForm,
          status: 'Open',
        }),
      })

      if (!response.ok) {
        throw new Error('Equipment issue request failed.')
      }

      setEquipmentForm(EMPTY_EQUIPMENT_FORM)
      setCreateMessage('Equipment issue created.')
      await loadDashboardData(selectedDate)
    } catch {
      setCreateError('Could not create the equipment issue. Please check the required fields and try again.')
    } finally {
      setCreateLoading('')
    }
  }

  async function importCsv(file, endpoint, setLoadingState, setResult, setImportError) {
    if (!file) {
      setImportError('Please choose a CSV file first.')
      return
    }

    setLoadingState(true)
    setResult('')
    setImportError('')

    const formData = new FormData()
    formData.append('file', file)

    try {
      const response = await fetch(`${API_BASE_URL}${endpoint}`, {
        method: 'POST',
        body: formData,
      })

      const syncRun = await response.json()

      if (!response.ok || syncRun.status === 'Failed') {
        throw new Error(syncRun.message || 'Import failed.')
      }

      setResult(`Import completed. ${syncRun.recordsProcessed ?? 0} record(s) processed.`)
      await loadDashboardData(selectedDate)
    } catch (error) {
      setImportError(error.message || 'Import failed. Please check the file and try again.')
    } finally {
      setLoadingState(false)
    }
  }

  async function calculateGddRange() {
    setGddRangeLoading(true)
    setGddRangeResult(null)
    setGddRangeError('')

    try {
      const response = await fetch(
        `${API_BASE_URL}/api/gdd/range?startDate=${gddStartDate}&endDate=${gddEndDate}`,
      )

      if (!response.ok) {
        throw new Error('GDD range request failed.')
      }

      setGddRangeResult(await response.json())
    } catch {
      setGddRangeError('Could not calculate GDD for that range. Please check the dates and try again.')
    } finally {
      setGddRangeLoading(false)
    }
  }

  async function loadTrends(dateToLoad = selectedDate) {
    setTrendsLoading(true)
    setTrendsError('')

    try {
      const [roundsResponse, revenueResponse, gddResponse] = await Promise.all([
        fetch(`${API_BASE_URL}/api/trends/rounds?days=7&endDate=${dateToLoad}`),
        fetch(`${API_BASE_URL}/api/trends/revenue?days=7&endDate=${dateToLoad}`),
        fetch(`${API_BASE_URL}/api/trends/gdd?days=30&endDate=${dateToLoad}`),
      ])

      if (!roundsResponse.ok || !revenueResponse.ok || !gddResponse.ok) {
        throw new Error('Trend request failed.')
      }

      const [rounds, revenue, gdd] = await Promise.all([
        roundsResponse.json(),
        revenueResponse.json(),
        gddResponse.json(),
      ])

      setTrends({ rounds, revenue, gdd })
    } catch {
      setTrendsError('Could not load trend charts. Please make sure the API is running and try again.')
    } finally {
      setTrendsLoading(false)
    }
  }

  async function loadForecast(dateToLoad) {
    setForecastLoading(true)
    setForecastError('')
    setForecast(null)

    try {
      const response = await fetch(`${API_BASE_URL}/api/forecast?date=${dateToLoad}`)

      if (!response.ok) {
        throw new Error('Forecast request failed.')
      }

      setForecast(await response.json())
    } catch {
      setForecastError('Could not load the forecast right now.')
    } finally {
      setForecastLoading(false)
    }
  }

  async function loadAdminData(dateToLoad = selectedDate) {
    setAdminLoading(true)
    setAdminError('')

    try {
      const [payrollResponse, remindersResponse] = await Promise.all([
        fetch(`${API_BASE_URL}/api/admin/payroll-current?date=${dateToLoad}`),
        fetch(`${API_BASE_URL}/api/admin/reminders`),
      ])

      if (!payrollResponse.ok || !remindersResponse.ok) {
        throw new Error('Admin request failed.')
      }

      const [payroll, reminders] = await Promise.all([
        payrollResponse.json(),
        remindersResponse.json(),
      ])

      setPayrollSummary(payroll)
      setAdminReminders(reminders)
    } catch {
      setAdminError('Could not load admin and payroll deadlines.')
    } finally {
      setAdminLoading(false)
    }
  }

  async function submitPayrollPeriod(periodId) {
    setAdminActionLoading('payroll-submit')
    setAdminError('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/payroll-periods/${periodId}/submit`, {
        method: 'PUT',
      })

      if (!response.ok) {
        throw new Error('Payroll submit request failed.')
      }

      await loadDashboardData(selectedDate)
    } catch {
      setAdminError('Could not mark payroll as submitted.')
    } finally {
      setAdminActionLoading('')
    }
  }

  async function completeAdminReminder(reminderId) {
    setAdminActionLoading(`reminder-${reminderId}`)
    setAdminError('')

    try {
      const response = await fetch(`${API_BASE_URL}/api/admin/reminders/${reminderId}/complete`, {
        method: 'PUT',
      })

      if (!response.ok) {
        throw new Error('Reminder complete request failed.')
      }

      await loadDashboardData(selectedDate)
    } catch {
      setAdminError('Could not complete the reminder.')
    } finally {
      setAdminActionLoading('')
    }
  }

  const insights = dashboard?.insights || []
  const alerts = dashboard?.alerts || []
  const priorities = dashboard?.priorities || []

  return (
    <main className="dashboard-page">
      <header className="dashboard-header">
        <div className="header-copy">
          <p className="eyebrow">Course Commander</p>
          <h1>Dashboard</h1>
        </div>

        <div className="date-controls">
          <label htmlFor="dashboard-date">Date</label>
          <div className="date-row">
            <input
              id="dashboard-date"
              type="date"
              value={selectedDate}
              onChange={(event) => setSelectedDate(event.target.value)}
            />
            <button type="button" onClick={handleGoToToday} disabled={!selectedDate}>
              Go to Today
            </button>
          </div>
        </div>
      </header>

      <section className="summary-header">
        <p>{formatDisplayDate(selectedDate)}</p>
        <h2>Operations Summary</h2>
      </section>

      <DemoModeControl
        status={demoStatus}
        loading={demoLoading}
        message={demoMessage}
        error={demoError}
        onLoadDemo={() => updateDemoData('/api/demo/load', 'POST', 'Sample data loaded.')}
        onClearDemo={() => updateDemoData('/api/demo/clear', 'DELETE', 'Sample data cleared.')}
      />

      {loading && <p className="status-message">Loading dashboard...</p>}
      {error && <p className="status-message error">{error}</p>}
      {!error && !dashboard && !loading && (
        <p className="status-message">Select a date to view the dashboard.</p>
      )}

      {dashboard?.dailyBriefing && dashboard.briefingMode === 'TodayOutlook' && (
        <div className="briefing-grid">
          <BriefingBox title="Today's Outlook" text={dashboard.dailyBriefing} variant="outlook" />
          <BriefingBox title="Yesterday's Recap" text={dashboard.yesterdayRecap} variant="recap" />
        </div>
      )}

      {dashboard?.dailyBriefing && dashboard.briefingMode !== 'TodayOutlook' && (
        <BriefingBox title="Daily Briefing" text={dashboard.dailyBriefing} />
      )}

      {dashboard && (
        <>
          <div className="first-screen-grid">
          <section className="dashboard-section priority-section">
            <h2>Today's Priorities</h2>
            <PriorityList priorities={priorities} />
          </section>

          <section className="dashboard-section action-section">
            <h2>Action Items</h2>
            {alerts.length === 0 ? (
              <p className="empty-message">No urgent action items for this date.</p>
            ) : (
              <div className="action-grid">
                {alerts.map((alert, index) => (
                  <AlertCard alert={alert} key={`${alert.title || alert.Title}-${index}`} />
                ))}
              </div>
            )}
          </section>
          </div>

          <ForecastCard forecast={forecast} loading={forecastLoading} error={forecastError} />

          <div className="dashboard-content">
          <AdminPayrollSection
            payrollSummary={payrollSummary}
            reminders={adminReminders}
            loading={adminLoading}
            error={adminError}
            actionLoading={adminActionLoading}
            onSubmitPayroll={submitPayrollPeriod}
            onCompleteReminder={completeAdminReminder}
          />

          <CreateTaskIssueSection
            maintenanceForm={maintenanceForm}
            equipmentForm={equipmentForm}
            createLoading={createLoading}
            createMessage={createMessage}
            createError={createError}
            onMaintenanceChange={updateMaintenanceForm}
            onEquipmentChange={updateEquipmentForm}
            onCreateMaintenance={createMaintenanceTask}
            onCreateEquipment={createEquipmentIssue}
          />

          <section className="dashboard-section">
            <h2>Task Management</h2>
            {taskManagementLoading && <p className="status-message">Loading active tasks...</p>}
            {taskManagementError && <p className="status-message error">{taskManagementError}</p>}
            {!taskManagementLoading && (
              <div className="task-management-grid">
                <TaskManagementList
                  title="Maintenance Tasks"
                  items={maintenanceTasks}
                  type="maintenance"
                  actionLoading={taskActionLoading}
                  onAction={updateTaskStatus}
                />
                <TaskManagementList
                  title="Equipment Issues"
                  items={equipmentIssues}
                  type="equipment"
                  actionLoading={taskActionLoading}
                  onAction={updateTaskStatus}
                />
              </div>
            )}
          </section>

          <DashboardSection title="Daily Operations">
            <StatCard label="Rounds Played" value={formatValue(dashboard.roundsPlayed)} />
            <StatCard label="Cart Rentals" value={formatValue(dashboard.cartRentals)} />
            <StatCard label="Total Revenue" value={formatCurrency(dashboard.totalRevenue)} />
            <StatCard label="Weather Summary" value={formatValue(dashboard.weatherSummary)} />
          </DashboardSection>

          <DashboardSection title="F&B Performance">
            <StatCard
              label="F&B Revenue"
              value={formatCurrency(dashboard.fandBAnalytics?.foodAndBeverageRevenue)}
            />
            <StatCard
              label="F&B Revenue / Round"
              value={formatCurrency(dashboard.fandBAnalytics?.fandBRevenuePerRound)}
            />
            <StatCard
              label="Alcohol Share"
              value={formatPercent(dashboard.fandBAnalytics?.alcoholSharePercent)}
            />
            <StatCard
              label="Range Ball Revenue"
              value={formatCurrency(dashboard.fandBAnalytics?.rangeBallRevenue)}
            />
          </DashboardSection>

          <section className="dashboard-section">
            <h2>Weather & Turf</h2>
            <div className="section-grid">
              <StatCard label="Today's GDD" value={formatGdd(dashboard.dailyGdd?.gdd)} />
              <StatCard label="Past 30 Days GDD" value={formatGdd(dashboard.past30DaysGdd?.totalGdd)} />
              <StatCard label="Year-to-Date GDD" value={formatGdd(dashboard.yearToDateGdd?.totalGdd)} />
              <StatCard
                label="Average Moisture"
                value={formatMoisture(dashboard.turfConditions?.averageMoistureToday)}
              />
              <StatCard
                label="Lowest Moisture"
                value={formatMoisture(dashboard.turfConditions?.lowestMoistureReading)}
                alert={(dashboard.turfConditions?.lowestMoistureReading ?? 100) < 15}
              />
              <StatCard
                label="Highest Moisture"
                value={formatMoisture(dashboard.turfConditions?.highestMoistureReading)}
                alert={(dashboard.turfConditions?.highestMoistureReading ?? 0) > 30}
              />
            </div>

            <div className="turf-detail-card">
              <h3>Top 3 Driest Locations</h3>
              <DriestLocationsList locations={dashboard.turfConditions?.topDriestLocations} />
            </div>

            <div className="gdd-range-form">
              <div className="gdd-field">
                <label htmlFor="gdd-start-date">Start Date</label>
                <input
                  id="gdd-start-date"
                  type="date"
                  value={gddStartDate}
                  onChange={(event) => setGddStartDate(event.target.value)}
                />
              </div>

              <div className="gdd-field">
                <label htmlFor="gdd-end-date">End Date</label>
                <input
                  id="gdd-end-date"
                  type="date"
                  value={gddEndDate}
                  onChange={(event) => setGddEndDate(event.target.value)}
                />
              </div>

              <button
                type="button"
                onClick={calculateGddRange}
                disabled={gddRangeLoading || !gddStartDate || !gddEndDate}
              >
                {gddRangeLoading ? 'Calculating...' : 'Calculate GDD'}
              </button>
            </div>

            {gddRangeError && <p className="import-message error">{gddRangeError}</p>}

            {gddRangeResult && (
              <div className="gdd-result-grid">
                <StatCard label="Range Total GDD" value={formatGdd(gddRangeResult.totalGdd)} />
                <StatCard label="Average Daily GDD" value={formatGdd(gddRangeResult.averageDailyGdd)} />
                <StatCard label="Days Included" value={gddRangeResult.daysIncluded ?? 0} />
              </div>
            )}
          </section>

          <section className="dashboard-section">
            <h2>Trends</h2>
            {trendsLoading && <p className="status-message">Loading trend charts...</p>}
            {trendsError && <p className="status-message error">{trendsError}</p>}
            {!trendsLoading && !trendsError && (
              <div className="trend-grid">
                <TrendChart
                  title="Rounds Trend"
                  label="Rounds Played"
                  points={trends.rounds}
                  color="#206a42"
                />
                <TrendChart
                  title="Revenue Trend"
                  label="Revenue ($)"
                  points={trends.revenue}
                  color="#2563eb"
                />
                <TrendChart
                  title="GDD Trend"
                  label="GDD"
                  points={trends.gdd}
                  color="#c45a1c"
                />
              </div>
            )}
          </section>

          <DashboardSection title="Maintenance">
            <StatCard label="Open Maintenance Tasks" value={dashboard.openMaintenanceTaskCount ?? 0} />
            <StatCard
              label="Critical Maintenance Tasks"
              value={dashboard.criticalMaintenanceTaskCount ?? 0}
              alert={(dashboard.criticalMaintenanceTaskCount ?? 0) > 0}
            />
          </DashboardSection>

          <DashboardSection title="Equipment Issues">
            <StatCard label="Open Equipment Issues" value={dashboard.openEquipmentIssueCount ?? 0} />
            <StatCard
              label="Critical Equipment Issues"
              value={dashboard.criticalEquipmentIssueCount ?? 0}
              alert={(dashboard.criticalEquipmentIssueCount ?? 0) > 0}
            />
          </DashboardSection>

          <section className="dashboard-section">
            <h2>Data Import</h2>
            <div className="import-grid">
              <ImportControl
                label="Upload Play CSV"
                buttonText="Import Play Data"
                file={playCsvFile}
                loading={playImportLoading}
                result={playImportResult}
                error={playImportError}
                onFileChange={(event) => setPlayCsvFile(event.target.files[0] || null)}
                onImport={() =>
                  importCsv(
                    playCsvFile,
                    '/api/integrations/play/import-csv',
                    setPlayImportLoading,
                    setPlayImportResult,
                    setPlayImportError,
                  )
                }
              />

              <ImportControl
                label="Upload Sales CSV"
                buttonText="Import Sales Data"
                file={salesCsvFile}
                loading={salesImportLoading}
                result={salesImportResult}
                error={salesImportError}
                onFileChange={(event) => setSalesCsvFile(event.target.files[0] || null)}
                onImport={() =>
                  importCsv(
                    salesCsvFile,
                    '/api/integrations/sales/import-csv',
                    setSalesImportLoading,
                    setSalesImportResult,
                    setSalesImportError,
                  )
                }
              />
            </div>
          </section>

          <section className="dashboard-section">
            <h2>Insights</h2>
            <div className="insight-list">
              {insights.length === 0 && <p className="empty-message">No insights available.</p>}
              {insights.map((insight, index) => {
                const message = getInsightMessage(insight)
                const severity = getInsightSeverity(message)

                return (
                  <div className={`insight-card ${severity}`} key={insight.id || index}>
                    {cleanDisplayText(message)}
                  </div>
                )
              })}
            </div>
          </section>
        </div>
        </>
      )}
    </main>
  )
}

export default Dashboard
