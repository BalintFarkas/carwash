﻿import React from 'react';
import PropTypes from 'prop-types';
import TrackedComponent from './TrackedComponent';
import { Redirect } from 'react-router';
import apiFetch from '../Auth';
import { withStyles } from '@material-ui/core/styles';
import Stepper from '@material-ui/core/Stepper';
import Step from '@material-ui/core/Step';
import StepLabel from '@material-ui/core/StepLabel';
import StepContent from '@material-ui/core/StepContent';
import Button from '@material-ui/core/Button';
import Chip from '@material-ui/core/Chip';
import Typography from '@material-ui/core/Typography';
import Radio from '@material-ui/core/Radio';
import RadioGroup from '@material-ui/core/RadioGroup';
import FormGroup from '@material-ui/core/FormGroup';
import Checkbox from '@material-ui/core/Checkbox';
import FormControlLabel from '@material-ui/core/FormControlLabel';
import FormControl from '@material-ui/core/FormControl';
import CircularProgress from '@material-ui/core/CircularProgress';
import TextField from '@material-ui/core/TextField';
import InputLabel from '@material-ui/core/InputLabel';
import MenuItem from '@material-ui/core/MenuItem';
import Select from '@material-ui/core/Select';
import InfiniteCalendar from 'react-infinite-calendar';
import CloudOffIcon from '@material-ui/icons/CloudOff';
import ErrorOutlineIcon from '@material-ui/icons/ErrorOutline';
import { Garages } from './Constants';
import 'react-infinite-calendar/styles.css';
import './Reserve.css';

const styles = theme => ({
    stepper: {
        padding: 0,
        backgroundColor: 'inherit',
    },
    button: {
        marginTop: theme.spacing.unit,
        marginRight: theme.spacing.unit,
    },
    actionsContainer: {
        marginTop: theme.spacing.unit,
        marginBottom: theme.spacing.unit * 2,
    },
    resetContainer: {
        padding: theme.spacing.unit * 3,
    },
    chip: {
        margin: theme.spacing.unit / 2,
    },
    selectedChip: {
        margin: theme.spacing.unit / 2,
        backgroundColor: theme.palette.primary.main,
        '&:hover': {
            backgroundColor: theme.palette.primary.dark,
        },
        '&:focus': {
            backgroundColor: theme.palette.primary.main,
        },
        '&:hover:focus': {
            backgroundColor: theme.palette.primary.dark,
        },
    },
    chipGroupTitle: {
        marginTop: theme.spacing.unit / 2,
    },
    calendar: {
        maxWidth: '400px',
    },
    radioGroup: {
        margin: `${theme.spacing.unit}px 0`,
    },
    container: {
        display: 'flex',
        flexWrap: 'wrap',
    },
    textField: {
        marginLeft: theme.spacing.unit,
        marginRight: theme.spacing.unit,
        [theme.breakpoints.down('sm')]: {
            width: '100%',
        },
        [theme.breakpoints.up('md')]: {
            width: 200,
        },
    },
    checkbox: {
        marginLeft: theme.spacing.unit,
        marginRight: theme.spacing.unit,
    },
    formControl: {
        margin: theme.spacing.unit,
        [theme.breakpoints.down('sm')]: {
            width: '100%',
        },
        [theme.breakpoints.up('md')]: {
            width: 200,
        },
    },
    progress: {
        margin: theme.spacing.unit * 2,
    },
    center: {
        display: 'grid',
        placeItems: 'center',
        textAlign: 'center',
        height: '80%',
    },
    errorIcon: {
        margin: theme.spacing.unit,
        color: '#BDBDBD',
        width: '100px',
        height: '100px',
    },
    errorText: {
        color: '#9E9E9E',
    },
});

const timeFormat = new Intl.DateTimeFormat('en-US', {
    hour: '2-digit',
    minute: '2-digit',
});
const dateFormat = new Intl.DateTimeFormat('en-US', {
    year: 'numeric',
    month: 'long',
    day: '2-digit',
});

function addDays(date, days) {
    const newDate = new Date(date);
    newDate.setDate(newDate.getDate() + days);
    return newDate;
}

class Reserve extends TrackedComponent {
    displayName = Reserve.name;

    constructor(props) {
        super(props);
        this.state = {
            activeStep: 0,
            notAvailableDates: [],
            notAvailableTimes: [],
            loading: true,
            loadingReservation: false,
            reservationCompleteRedirect: false,
            services: [
                { id: 0, name: 'exterior', selected: false },
                { id: 1, name: 'interior', selected: false },
                { id: 2, name: 'carpet', selected: false },
                { id: 3, name: 'spot cleaning', selected: false },
                { id: 4, name: 'vignette removal', selected: false },
                { id: 5, name: 'polishing', selected: false },
                { id: 6, name: "AC cleaning 'ozon'", selected: false },
                { id: 7, name: "AC cleaning 'bomba'", selected: false },
            ],
            validationErrors: {
                vehiclePlateNumber: false,
                garage: false,
                floor: false,
            },
            selectedDate: new Date(),
            vehiclePlateNumber: '',
            garage: '',
            floor: '',
            seat: '',
            private: false,
            comment: '',
            disabledSlots: [],
            reservationPrecentage: [],
            reservationPrecentageDataArrived: false,
            users: [],
            userId: this.props.user.id,
            servicesStepLabel: 'Select services',
            dateStepLabel: 'Choose date',
            timeStepLabel: 'Choose time',
            locationKnown: false,
        };
    }

    componentDidMount() {
        super.componentDidMount();

        if (this.props.match.params.id) {
            this.setState({
                loadingReservation: true,
            });
            apiFetch(`api/reservations/${this.props.match.params.id}`).then(
                data => {
                    const services = this.state.services;
                    data.services.forEach(s => {
                        services[s].selected = true;
                    });
                    const [garage, floor, seat] = data.location.split('/');
                    const date = new Date(data.startDate);
                    this.setState({
                        services,
                        selectedDate: date,
                        vehiclePlateNumber: data.vehiclePlateNumber,
                        garage,
                        floor,
                        seat,
                        private: data.private,
                        comment: data.comment,
                        servicesStepLabel: services
                            .filter(s => s.selected)
                            .map(s => s.name)
                            .join(', '),
                        dateStepLabel: dateFormat.format(date),
                        timeStepLabel: timeFormat.format(date),
                        loadingReservation: false,
                    });
                },
                error => {
                    this.setState({ loading: false });
                    this.props.openSnackbar(error);
                }
            );
        } else {
            apiFetch('api/reservations/lastsettings').then(
                data => {
                    if (Object.keys(data).length !== 0) {
                        const [garage, floor] = data.location.split('/');
                        this.setState({
                            vehiclePlateNumber: data.vehiclePlateNumber,
                            garage,
                            floor,
                        });
                    }
                },
                error => {
                    this.setState({ loading: false });
                    this.props.openSnackbar(error);
                }
            );
        }

        if (this.props.user.isCarwashAdmin) {
            this.setState({
                loading: false,
            });
        } else {
            apiFetch('api/reservations/notavailabledates').then(
                data => {
                    const { dates, times } = data;
                    for (const i in dates) {
                        if (dates.hasOwnProperty(i)) {
                            dates[i] = new Date(dates[i]);
                        }
                    }
                    for (const i in times) {
                        if (times.hasOwnProperty(i)) {
                            times[i] = new Date(times[i]);
                        }
                    }
                    this.setState({
                        notAvailableDates: dates,
                        notAvailableTimes: times,
                        loading: false,
                    });
                },
                error => {
                    this.setState({ loading: false });
                    this.props.openSnackbar(error);
                }
            );
        }

        if (this.props.user.isAdmin) {
            apiFetch('api/users/dictionary').then(
                data => {
                    this.setState({
                        users: data,
                    });
                },
                error => {
                    this.setState({ loading: false });
                    this.props.openSnackbar(error);
                }
            );
        }
    }

    isUserConcurrentReservationLimitMet = () => {
        if (this.props.match.params.id) return false;
        if (this.props.user.isAdmin || this.props.user.isCarwashAdmin) return false;
        return this.props.reservations.filter(r => r.state !== 5).length >= 2;
    };

    handleNext = () => {
        this.setState(state => ({
            activeStep: state.activeStep + 1,
        }));
    };

    handleBack = () => {
        this.setState(state => ({
            activeStep: state.activeStep - 1,
            reservationPrecentageDataArrived: false,
        }));
    };

    handleServiceChipClick = service => () => {
        this.setState(state => {
            const services = [...state.services];
            services[service.id].selected = !services[service.id].selected;

            // if carpet, must include exterior and interior too
            if (service.id === 2 && service.selected) {
                services[0].selected = true;
                services[1].selected = true;
            }
            if ((service.id === 0 || service.id === 1) && !service.selected) {
                services[2].selected = false;
            }

            return { services };
        });
    };

    handleServiceSelectionComplete = () => {
        this.setState(state => ({
            activeStep: 1,
            servicesStepLabel: state.services
                .filter(service => service.selected)
                .map(service => service.name)
                .join(', '),
        }));
    };

    handleDateSelectionComplete = date => {
        if (!date) return;

        this.setState({
            activeStep: 2,
            selectedDate: date,
            disabledSlots: [this.isTimeNotAvailable(date, 8), this.isTimeNotAvailable(date, 11), this.isTimeNotAvailable(date, 14)],
            dateStepLabel: dateFormat.format(date),
        });

        apiFetch(`api/reservations/reservationprecentage?date=${date.toJSON()}`).then(
            data => {
                this.setState({
                    reservationPrecentage: data,
                    reservationPrecentageDataArrived: true,
                });
            },
            error => {
                this.setState({ loading: false });
                this.props.openSnackbar(error);
            }
        );
    };

    getSlotReservationPrecentage = slot => {
        if (!this.state.reservationPrecentageDataArrived) return '';
        if (!this.state.reservationPrecentage[slot]) return '(0%)';
        return `(${this.state.reservationPrecentage[slot].precentage * 100}%)`;
    };

    handleTimeSelectionComplete = event => {
        const time = event.target.value;
        const dateTime = new Date(this.state.selectedDate);
        dateTime.setHours(time);
        this.setState({
            activeStep: 3,
            selectedDate: dateTime,
            timeStepLabel: timeFormat.format(dateTime),
        });
    };

    isTimeNotAvailable = (date, time) => {
        const dateTime = new Date(date);
        dateTime.setHours(time);
        return this.state.notAvailableTimes.filter(notAvailableTime => notAvailableTime.getTime() === dateTime.getTime()).length > 0;
    };

    handlePlateNumberChange = event => {
        this.setState({
            vehiclePlateNumber: event.target.value.toUpperCase(),
        });
    };

    handlePrivateChange = () => {
        this.setState(state => ({
            private: !state.private,
        }));
    };

    handleCommentChange = event => {
        this.setState({
            comment: event.target.value,
        });
    };

    handleLocationKnown = () => {
        this.setState(state => ({
            locationKnown: !state.locationKnown,
        }));
    };

    handleGarageChange = event => {
        this.setState({
            garage: event.target.value,
        });
    };

    handleFloorChange = event => {
        this.setState({
            floor: event.target.value,
        });
    };

    handleSeatChange = event => {
        this.setState({
            seat: event.target.value,
        });
    };

    handleUserChange = event => {
        this.setState({
            userId: event.target.value,
        });
    };

    handleReserve = () => {
        const validationErrors = {
            vehiclePlateNumber: this.state.vehiclePlateNumber === '',
            garage: this.state.garage === '',
            floor: this.state.floor === '',
        };

        if (validationErrors.vehiclePlateNumber || validationErrors.garage || validationErrors.floor) {
            this.setState({ validationErrors });
            return;
        }

        this.setState({
            loading: true,
        });

        const payload = {
            id: this.props.match.params.id,
            userId: this.state.userId,
            vehiclePlateNumber: this.state.vehiclePlateNumber,
            location: this.state.locationKnown ? `${this.state.garage}/${this.state.floor}/${this.state.seat}` : null,
            services: this.state.services.filter(s => s.selected).map(s => s.id),
            private: this.state.private,
            startDate: this.state.selectedDate,
            comment: this.state.comment,
        };

        let apiUrl = 'api/reservations';
        let apiMethod = 'POST';
        if (payload.id) {
            apiUrl = `api/reservations/${payload.id}`;
            apiMethod = 'PUT';
        }

        apiFetch(apiUrl, {
            method: apiMethod,
            body: JSON.stringify(payload),
            headers: {
                'Content-Type': 'application/json',
            },
        }).then(
            data => {
                this.setState({
                    loading: false,
                    reservationCompleteRedirect: true,
                });
                this.props.openSnackbar('Reservation successfully saved.');

                // Add new / update reservation to reservations
                if (apiMethod === 'PUT') this.props.removeReservation(data.id);
                this.props.addReservation(data);
            },
            error => {
                this.setState({ loading: false });
                this.props.openSnackbar(error);
            }
        );
    };

    render() {
        const { classes, user } = this.props;
        const {
            activeStep,
            servicesStepLabel,
            dateStepLabel,
            timeStepLabel,
            validationErrors,
            loading,
            loadingReservation,
            notAvailableDates,
            disabledSlots,
            users,
            userId,
            vehiclePlateNumber,
            comment,
            garage,
            floor,
            seat,
            locationKnown,
        } = this.state;
        const today = new Date();

        if (this.state.reservationCompleteRedirect) {
            return <Redirect to="/" />;
        }

        if (!navigator.onLine) {
            return (
                <div className={classes.center}>
                    <div>
                        <CloudOffIcon className={classes.errorIcon} />
                        <Typography variant="title" gutterBottom className={classes.errorText}>
                            Connect to the Internet
                        </Typography>
                        <Typography className={classes.errorText}>You must be connected to make a new reservation.</Typography>
                    </div>
                </div>
            );
        }

        if (this.isUserConcurrentReservationLimitMet()) {
            return (
                <div className={classes.center}>
                    <div>
                        <ErrorOutlineIcon className={classes.errorIcon} />
                        <Typography variant="title" gutterBottom className={classes.errorText}>
                            Limit reached
                        </Typography>
                        <Typography className={classes.errorText}>You cannot have more than two concurrent active reservations.</Typography>
                    </div>
                </div>
            );
        }

        return (
            <Stepper activeStep={activeStep} orientation="vertical" className={classes.stepper}>
                <Step>
                    <StepLabel>{servicesStepLabel}</StepLabel>
                    <StepContent>
                        {loadingReservation ? (
                            <CircularProgress className={classes.progress} size={50} />
                        ) : (
                            <React.Fragment>
                                {this.state.services.map(service => (
                                    <span key={service.id}>
                                        {service.id === 0 && <Typography variant="body2">Basic</Typography>}
                                        {service.id === 3 && <Typography variant="body2">Extras</Typography>}
                                        {service.id === 6 && <Typography variant="body2">AC</Typography>}
                                        <Chip
                                            key={service.id}
                                            label={service.name}
                                            onClick={this.handleServiceChipClick(service)}
                                            className={service.selected ? classes.selectedChip : classes.chip}
                                        />
                                        {service.id === 2 && <br />}
                                        {service.id === 5 && <br />}
                                    </span>
                                ))}
                                <div className={classes.actionsContainer}>
                                    <div>
                                        <Button disabled className={classes.button}>
                                            Back
                                        </Button>
                                        <Button
                                            variant="contained"
                                            color="primary"
                                            onClick={this.handleServiceSelectionComplete}
                                            className={classes.button}
                                            disabled={this.state.services.filter(service => service.selected === true).length <= 0}
                                        >
                                            Next
                                        </Button>
                                    </div>
                                </div>
                            </React.Fragment>
                        )}
                    </StepContent>
                </Step>
                <Step>
                    <StepLabel>{dateStepLabel}</StepLabel>
                    <StepContent>
                        {loading ? (
                            <CircularProgress className={classes.progress} size={50} />
                        ) : (
                            <InfiniteCalendar
                                onSelect={date => this.handleDateSelectionComplete(date)}
                                selected={null}
                                min={today}
                                minDate={today}
                                max={addDays(today, 365)}
                                locale={{ weekStartsOn: 1 }}
                                disabledDays={[0, 6, 7]}
                                disabledDates={notAvailableDates}
                                displayOptions={{ showHeader: false, showTodayHelper: false }}
                                width={'100%'}
                                height={350}
                                className={classes.calendar}
                                theme={{
                                    selectionColor: '#80d8ff',
                                    weekdayColor: '#80d8ff',
                                }}
                            />
                        )}
                        <div className={classes.actionsContainer}>
                            <div>
                                <Button onClick={this.handleBack} className={classes.button}>
                                    Back
                                </Button>
                                <Button disabled variant="contained" color="primary" className={classes.button}>
                                    Next
                                </Button>
                            </div>
                        </div>
                    </StepContent>
                </Step>
                <Step>
                    <StepLabel>{timeStepLabel}</StepLabel>
                    <StepContent>
                        <FormControl component="fieldset">
                            <RadioGroup aria-label="Time" name="time" className={classes.radioGroup} onChange={this.handleTimeSelectionComplete}>
                                <FormControlLabel
                                    value="8"
                                    control={<Radio />}
                                    label={`8:00 AM - 11:00 AM ${this.getSlotReservationPrecentage(0)}`}
                                    disabled={disabledSlots[0]}
                                />
                                <FormControlLabel
                                    value="11"
                                    control={<Radio />}
                                    label={`11:00 AM - 2:00 PM ${this.getSlotReservationPrecentage(1)}`}
                                    disabled={disabledSlots[1]}
                                />
                                <FormControlLabel
                                    value="14"
                                    control={<Radio />}
                                    label={`2:00 PM - 5:00 PM ${this.getSlotReservationPrecentage(2)}`}
                                    disabled={disabledSlots[2]}
                                />
                            </RadioGroup>
                        </FormControl>
                        <div className={classes.actionsContainer}>
                            <div>
                                <Button onClick={this.handleBack} className={classes.button}>
                                    Back
                                </Button>
                                <Button disabled variant="contained" color="primary" className={classes.button}>
                                    Next
                                </Button>
                            </div>
                        </div>
                    </StepContent>
                </Step>
                <Step>
                    <StepLabel>Reserve</StepLabel>
                    <StepContent>
                        {loading ? (
                            <CircularProgress className={classes.progress} size={50} />
                        ) : (
                            <div>
                                <div>
                                    <FormGroup className={classes.checkbox}>
                                        <FormControlLabel
                                            control={<Checkbox checked={this.state.private} onChange={this.handlePrivateChange} value="private" />}
                                            label="Private"
                                        />
                                    </FormGroup>
                                </div>
                                {user.isAdmin && (
                                    <FormControl className={classes.formControl}>
                                        <InputLabel htmlFor="user">User</InputLabel>
                                        <Select
                                            required
                                            value={userId}
                                            onChange={this.handleUserChange}
                                            inputProps={{
                                                name: 'user',
                                                id: 'user',
                                            }}
                                        >
                                            {Object.keys(users).map(id => (
                                                <MenuItem value={id} key={id}>
                                                    {users[id]}
                                                </MenuItem>
                                            ))}
                                        </Select>
                                    </FormControl>
                                )}
                                <div>
                                    <TextField
                                        required
                                        error={validationErrors.vehiclePlateNumber}
                                        id="vehiclePlateNumber"
                                        label="Plate number"
                                        value={vehiclePlateNumber}
                                        className={classes.textField}
                                        margin="normal"
                                        onChange={this.handlePlateNumberChange}
                                    />
                                </div>
                                <div>
                                    <FormGroup className={classes.checkbox} style={{ marginTop: 8 }}>
                                        <FormControlLabel
                                            control={
                                                <Checkbox checked={locationKnown} onChange={this.handleLocationKnown} value="locationKnown" color="primary" />
                                            }
                                            label="I have already parked the car"
                                        />
                                    </FormGroup>
                                </div>
                                {locationKnown && (
                                    <React.Fragment>
                                        <FormControl className={classes.formControl} error={validationErrors.garage}>
                                            <InputLabel htmlFor="garage">Garage</InputLabel>
                                            <Select
                                                required
                                                value={garage}
                                                onChange={this.handleGarageChange}
                                                inputProps={{
                                                    name: 'garage',
                                                    id: 'garage',
                                                }}
                                            >
                                                <MenuItem value="M">M</MenuItem>
                                                <MenuItem value="G">G</MenuItem>
                                                <MenuItem value="S1">S1</MenuItem>
                                            </Select>
                                        </FormControl>
                                        {garage && (
                                            <FormControl className={classes.formControl} error={validationErrors.floor}>
                                                <InputLabel htmlFor="floor">Floor</InputLabel>
                                                <Select
                                                    required
                                                    value={floor}
                                                    onChange={this.handleFloorChange}
                                                    inputProps={{
                                                        name: 'floor',
                                                        id: 'floor',
                                                    }}
                                                >
                                                    {Garages[garage].map(item => (
                                                        <MenuItem value={item} key={item}>
                                                            {item}
                                                        </MenuItem>
                                                    ))}
                                                </Select>
                                            </FormControl>
                                        )}
                                        {floor && (
                                            <TextField
                                                id="seat"
                                                label="Seat (optional)"
                                                value={seat}
                                                className={classes.textField}
                                                margin="normal"
                                                onChange={this.handleSeatChange}
                                            />
                                        )}
                                    </React.Fragment>
                                )}
                                <div>
                                    <TextField
                                        id="comment"
                                        label="Comment"
                                        multiline
                                        rowsMax="4"
                                        value={comment}
                                        onChange={this.handleCommentChange}
                                        className={classes.textField}
                                        margin="normal"
                                    />
                                </div>
                                <div className={classes.actionsContainer}>
                                    <div>
                                        <Button onClick={this.handleBack} className={classes.button}>
                                            Back
                                        </Button>
                                        <Button variant="contained" color="primary" onClick={this.handleReserve} className={classes.button}>
                                            {!this.props.match.params.id ? 'Reserve' : 'Update'}
                                        </Button>
                                    </div>
                                </div>
                            </div>
                        )}
                    </StepContent>
                </Step>
            </Stepper>
        );
    }
}

Reserve.propTypes = {
    classes: PropTypes.object.isRequired,
    user: PropTypes.object.isRequired,
    reservations: PropTypes.arrayOf(PropTypes.object).isRequired,
    addReservation: PropTypes.func.isRequired,
    removeReservation: PropTypes.func,
    openSnackbar: PropTypes.func.isRequired,
};

export default withStyles(styles)(Reserve);
