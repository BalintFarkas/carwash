import React from 'react';
import PropTypes from 'prop-types';
import TrackedComponent from './TrackedComponent';
import CarwashGrid from './CarwashGrid';

export default class CarwashAdmin extends TrackedComponent {
    displayName = CarwashAdmin.name;

    render() {
        const { backlog, backlogLoading, openSnackbar, updateBacklogItem } = this.props;

        return <CarwashGrid backlog={backlog} backlogLoading={backlogLoading} openSnackbar={openSnackbar} updateBacklogItem={updateBacklogItem} />;
    }
}

CarwashAdmin.propTypes = {
    backlog: PropTypes.arrayOf(PropTypes.object).isRequired,
    backlogLoading: PropTypes.bool.isRequired,
    openSnackbar: PropTypes.func.isRequired,
    updateBacklogItem: PropTypes.func.isRequired,
};
