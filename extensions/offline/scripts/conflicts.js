var responseHelper = require('../shared/responseHelper.js');

exports.resolveConflictOnServer = function (currentItem, newItem, resolveStrategy) {
    return resolveStrategy(currentItem, newItem);
}

exports.resolveConflictOnClient = function (request, currentItem, newItem) {
    var type = conf.UpdateUpdate;
    if (current.isDeleted && newItem.isDeleted) {
        type = conflictType.DeleteDelete;
    }
    else if (currentItem.isDeleted && !newItem.isDeleted) {
        type = conflictType.DeleteUpdate;
    }
    else if (!currentItem.isDeleted && newItem.isDeleted) {
        type = conflictType.UpdateDelete;
    }
    responseHelper.sendConflictResponse(request, type, currentItem.__version, currentItem, newItem, {});
}

conflictType = {
    ///<field name="UpdateUpdate" type="Number">Both the server and the local object have been changed.</field>
    UpdateUpdate: 0,
    ///<field name="UpdateDelete" type="Number">The server object has changed while the local object was deleted.</field>
    UpdateDelete: 1,
    ///<field name="DeleteUpdate" type="Number">The server object was deleted while the local object has changed.</field>
    DeleteUpdate: 2,
    ///<field name="DeleteDelete" type="Number">Both the server and the local object have been deleted.</field>
    DeleteDelete: 3,
}


// strategies

// lastest write wins strategy
exports.latestWriteWins = function (currentItem, newItem) {
    return newItem;
}

// server wins strategy
exports.serverWins = function (currentItem, newItem) {
    return currentItem;
}

// server wins strategy
exports.duplicationApply = function (currentItem, newItem) {
    delete newItem.id;
    return [currentItem, newItem];
}