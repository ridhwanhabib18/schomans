import { HubConnectionBuilder, LogLevel, type HubConnection } from '@microsoft/signalr'
import { config } from '@/shared/lib/config'

export const NOTIFICATION_CREATED = 'NotificationCreated'

/** What the server pushes when a notification is created for the signed-in user. */
export interface NotificationPush {
  id: string
  type: string
  title: string
  body: string
  linkUrl: string
  createdAt: string
}

/**
 * The SignalR connection of the notification hub. The token is read again whenever the connection (re)starts, so a
 * refreshed token is used; browsers cannot set headers on a WebSocket, so SignalR sends it in the query string.
 */
export function createNotificationConnection(getToken: () => string | null): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${config.apiUrl}/hubs/notifications`, { accessTokenFactory: () => getToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}

/** Events of the attendance hub, pushed to the screen of a session the user joined. */
export const ATTENDANCE_EVENTS = {
  studentCheckedIn: 'StudentCheckedIn',
  qrRotated: 'QrRotated',
  sessionClosed: 'SessionClosed',
} as const

/** The SignalR connection of the attendance hub. After connecting, call `JoinSession(sessionId)` to receive its events. */
export function createAttendanceConnection(getToken: () => string | null): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${config.apiUrl}/hubs/attendance`, { accessTokenFactory: () => getToken() ?? '' })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}
