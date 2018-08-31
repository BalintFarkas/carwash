export const State = Object.freeze({
    SubmittedNotActual: 0,
    ReminderSentWaitingForKey: 1,
    CarKeyLeftAndLocationConfirmed: 2,
    WashInProgress: 3,
    NotYetPaid: 4,
    Done: 5,
});

export const Service = Object.freeze({
    Exterior: 0,
    Interior: 1,
    Carpet: 2,
    SpotCleaning: 3,
    VignetteRemoval: 4,
    Polishing: 5,
    AcCleaningOzon: 6,
    AcCleaningBomba: 7,
    // below are those services that are hidden from the user
    BugRemoval: 8,
    WheelCleaning: 9,
    TireCare: 10,
    LeatherCare: 11,
    PlasticCare: 12,
    PreWash: 13,
});

export const Garages = {
    M: ['-1', '-2', '-2.5', '-3', '-3.5', 'outdoor'],
    G: ['-1', 'outdoor'],
    S1: ['-1', '-2', '-3'],
};

export function getStateName(state) {
    switch (state) {
        case State.SubmittedNotActual:
            return 'Scheduled';
        case State.ReminderSentWaitingForKey:
            return 'Leave the key at reception';
        case State.CarKeyLeftAndLocationConfirmed:
            return 'Waiting';
        case State.WashInProgress:
            return 'Wash in progress';
        case State.NotYetPaid:
            return 'You need to pay';
        case State.Done:
            return 'Done';
        default:
            return 'No info';
    }
}

export function getAdminStateName(state) {
    switch (state) {
        case State.SubmittedNotActual:
            return 'Scheduled';
        case State.ReminderSentWaitingForKey:
            return 'Waiting for key';
        case State.CarKeyLeftAndLocationConfirmed:
            return 'Queued';
        case State.WashInProgress:
            return 'In progress';
        case State.NotYetPaid:
            return 'Waiting for payment';
        case State.Done:
            return 'Done';
        default:
            return 'No info';
    }
}

export function getServiceName(service) {
    switch (service) {
        case Service.Exterior:
            return 'exterior';
        case Service.Interior:
            return 'interior';
        case Service.Carpet:
            return 'carpet';
        case Service.SpotCleaning:
            return 'spot cleaning';
        case Service.VignetteRemoval:
            return 'vignette removal';
        case Service.Polishing:
            return 'polishing';
        case Service.AcCleaningOzon:
            return "AC cleaning 'ozon'";
        case Service.AcCleaningBomba:
            return "AC cleaning 'bomba'";
        case Service.BugRemoval:
            return 'bug removal';
        case Service.WheelCleaning:
            return 'wheel cleaning';
        case Service.TireCare:
            return 'tire care';
        case Service.LeatherCare:
            return 'leather care';
        case Service.PlasticCare:
            return 'plastic care';
        case Service.PreWash:
            return 'prewash';
        default:
            return 'no info';
    }
}
