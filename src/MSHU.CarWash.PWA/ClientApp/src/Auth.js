﻿import AuthenticationContext from 'adal-angular';

const adalConfig = {
    clientId: '6e291d40-2613-4a74-9af5-790eb496a828',
    endpoints: {
        api: '6e291d40-2613-4a74-9af5-790eb496a828',
    },
    cacheLocation: 'localStorage',
    extraQueryParameter: '',
};

const authorizedTenantIds = [
    'bca200e7-1765-4001-977f-5363e5f7a63a', // carwash
    '72f988bf-86f1-41af-91ab-2d7cd011db47', // microsoft
    '', // sap
    '', // graphisoft
];

// https://github.com/salvoravida/react-adal

let authContext = new AuthenticationContext(adalConfig);

function adalGetToken(resourceGuid) {
    return new Promise((resolve, reject) => {
        authContext.acquireToken(resourceGuid, (message, token, msg) => {
            if (!msg) resolve(token);
            else reject({ message, msg });
        });
    });
}

export function runWithAdal(app) {
    // it must run in iframe to for refreshToken (parsing hash and get token)
    authContext.handleWindowCallback();

    // prevent iframe double app !!!
    if (window === window.parent) {
        if (!authContext.isCallback(window.location.hash)) {
            if (!authContext.getCachedToken(authContext.config.clientId) || !authContext.getCachedUser()) {
                if (authContext.getCachedUser()) {
                    const user = authContext.getCachedUser();
                    adalConfig.extraQueryParameter = `login_hint=${encodeURIComponent(user.profile.upn)}`;
                    authContext = new AuthenticationContext(adalConfig);
                }
                authContext.login();
            } else {
                const user = authContext.getCachedUser();
                // console.log(user);
                if (authorizedTenantIds.filter(id => id === user.profile.tid).length > 0) {
                    app();
                } else {
                    console.log(`Tenant ${user.profile.tid} is not athorized to use this application!`);
                }
            }
        }
    }
}

export function signOut() {
    authContext.logOut();
}

/**
 * Parses the JSON returned by a network request
 * @param {object} response A response from a network request
 * @return {object} The parsed JSON, status from the response
 */
function parseJson(response) {
    // NoContent would throw a JSON parsing error
    if (response.status === 204) {
        return {
            status: response.status,
            ok: response.ok,
            json: {},
        };
    }

    return new window.Promise((resolve, reject) =>
        response
            .json()
            .then(json =>
                resolve({
                    status: response.status,
                    ok: response.ok,
                    json,
                })
            )
            .catch(error => reject(error.message))
    );
}

/**
 * Requests a URL, returning a promise
 * @param {string} url The URL we want to request
 * @param {object} options The options we want to pass to "fetch"
 * @return {Promise} The request promise
 */
export default function apiFetch(url, options) {
    return adalGetToken(adalConfig.endpoints.api).then(token => {
        const o = options || {};
        if (!o.headers) o.headers = {};
        o.headers.Authorization = `Bearer ${token}`;

        return new window.Promise((resolve, reject) => {
            window
                .fetch(url, o)
                .catch(error => {
                    if (!navigator.onLine) {
                        console.error('NETWORK ERROR: Disconnected from network.');
                        return reject('You are offline.');
                    }
                    console.error(`NETWORK ERROR: ${error.message}`);
                    return reject('Network error. Are you offline?');
                })
                .then(parseJson)
                .then(
                    response => {
                        if (response.ok) {
                            return resolve(response.json);
                        }
                        // extract the error from the server's json
                        switch (response.status) {
                            case 400:
                                console.error(`BAD REQUEST: ${response.json}`);
                                return reject(`An error has occured: ${response.json}`);
                            case 401:
                                console.error(`UNAUTHORIZED: ${response.json}`);
                                return reject('You are not authorized. Please refresh the page!');
                            case 403:
                                console.error(`UNAUTHORIZED: ${response.json}`);
                                return reject("You don't have permission!");
                            case 404:
                                console.error(`NOT FOUND: ${response.json}`);
                                return reject('Not found.');
                            case 500:
                                console.error(`SERVER ERROR: ${response.json}`);
                                return reject('A server error has occured.');
                            default:
                                console.error(`UNKNOWN ERROR: ${response.json}`);
                                return reject(`An error has occured. ${response.json}`);
                        }
                    },
                    error => {
                        console.error(`JSON PARSING ERROR: ${error}`);
                        return reject('A server error has occured.');
                    }
                )
                .catch(error => {
                    console.error(`NETWORK ERROR: ${error.message}`);
                    return reject('Network error. Are you offline?');
                });
        });
    });
}

export const getToken = () => authContext.getCachedToken(authContext.config.clientId);
