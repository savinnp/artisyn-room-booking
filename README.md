# Room booking

A small web app for booking meeting rooms in one office.

- The main page shows one day: rooms are columns, time (09:00–19:00) runs down the rows. Use the arrows, **Today** or the date picker to switch days.
- Create a booking by picking a room and entering a topic, the organizer's name, and start and end times. There is no sign-in; the organizer's name is plain text.
- Delete a booking with the **✕** button on it.
- The same actions are available through an HTTP API.

Four rooms (Oak, Maple, Pine, Cedar) are created on first start.

## Requirements

[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Nothing else: NuGet packages are restored automatically and the SQLite database file is created on first start.

## Run

From the repository root:

```sh
dotnet run --project src/RoomBooking.Web
```

Then open <http://localhost:5080>.

The database is the file `src/RoomBooking.Web/roombooking.db`. Delete it to start over with an empty schedule.

## Test

From the repository root:

```sh
dotnet test
```

- `tests/RoomBooking.Domain.Tests`: the booking rules.
- `tests/RoomBooking.Web.Tests`: the HTTP API, run in memory with `WebApplicationFactory` against a temporary SQLite file per test.

## Booking rules

- The start must be before the end, and a booking must last at least 15 minutes.
- A booking must be within working hours, 09:00–19:00, on a single day. Ending exactly at 19:00 is fine.
- Bookings of the same room must not overlap. Back-to-back bookings (one ends at 11:00, the next starts at 11:00) are allowed.
- A topic and the organizer's name are required (at most 200 and 100 characters).

If a rule is broken, the page and the API return a message that says which rule.

## HTTP API

| Method and path | Result |
|---|---|
| `GET /api/rooms` | `200` with `[{ "id", "name", "capacity" }]` |
| `GET /api/bookings?date=2026-11-25` | `200` with the bookings that start on that day; `400` if `date` is missing or not `yyyy-MM-dd` |
| `POST /api/bookings` | `201` with the created booking; `400` if a rule is broken or the room does not exist; `409` if it overlaps another booking |
| `DELETE /api/bookings/{id}` | `204`; `404` if there is no such booking |

A booking looks like this (the `POST` body is the same without `id`):

```json
{
  "id": 1,
  "roomId": 2,
  "title": "Sprint planning",
  "organizer": "Alex Kim",
  "start": "2026-11-25T10:00:00",
  "end": "2026-11-25T11:00:00"
}
```

Errors are [problem details](https://www.rfc-editor.org/rfc/rfc9457): `detail` holds the whole message and `errors` lists each broken rule.

```json
{
  "status": 409,
  "title": "Booking conflict",
  "detail": "The room is already booked from 10:00 to 11:00 (\"Sprint planning\").",
  "errors": ["The room is already booked from 10:00 to 11:00 (\"Sprint planning\")."]
}
```

## Assumptions

- **One time zone.** All times are the office's local time. The API takes and returns times without a UTC offset (`2026-11-25T10:00:00`) and rejects times that have one (`...Z`, `...+03:00`). Times are stored as they are, with no conversion. The server runs in the office's time zone, so "today" on the page is the server's date.
- Any day can be booked, including weekends and past days: the rules above do not restrict that.
- The schema is created with EF Core's `EnsureCreated`, without migrations. If the data model changes, delete the database file.
- The overlap check and the insert run in one SQLite write transaction, so two simultaneous requests cannot book the same slot.

## Project layout

```
src/RoomBooking.Domain        Booking rules and entities; no web or database dependencies
src/RoomBooking.Web           ASP.NET Core app: Blazor UI, HTTP API, EF Core + SQLite storage
tests/RoomBooking.Domain.Tests
tests/RoomBooking.Web.Tests
```
