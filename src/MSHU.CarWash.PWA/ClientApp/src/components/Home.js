import React, { Component } from 'react';
import { adalFetch } from '../Auth';
import ReservationCard from './ReservationCard';
import Grid from '@material-ui/core/Grid';
import PropTypes from 'prop-types';
import { withStyles } from '@material-ui/core/styles';

const styles = theme => ({
    card: {
        [theme.breakpoints.down('sm')]: {
            minWidth: '100%',
            maxWidth: '100%',
        },
        [theme.breakpoints.up('md')]: {
            minWidth: 'inherit',
            maxWidth: 'inherit',
        },
    },
});

class Home extends Component {
    displayName = Home.name

    constructor(props) {
        super(props);
        this.state = { reservations: [], loading: true };
    }

    componentDidMount() {
        adalFetch('api/reservations')
            .then(response => response.json())
            .then(data => {
                this.setState({ reservations: data, loading: false });
            });
    }

    render() {
        const { classes } = this.props;
        if (this.state.loading) {
            return (<p>Loading...</p>);
        } else {
            return (
                <Grid
                    container
                    direction="row"
                    justify="flex-start"
                    alignItems="flex-start"
                    spacing={16}
                    style={{ maxHeight: 'calc(100% - 24px - 16px)', overflow: 'auto' }}
                >
                    {this.state.reservations.map(reservation =>
                        <Grid item key={reservation.id} className={classes.card} >
                            <ReservationCard reservation={reservation}/>
                        </Grid>
                    )}
                </Grid>
            );
        }
    }
}

Home.propTypes = {
    classes: PropTypes.object.isRequired,
};

export default withStyles(styles)(Home);