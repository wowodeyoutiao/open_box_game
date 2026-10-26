package db

import (
	"nanoserver/db/model"
	"nanoserver/pkg/errutil"
)

// 查询记录
func QueryPvpRecords(uid int64) ([]*model.PvpRecord, error) {
	if uid <= 0 {
		return nil, errutil.ErrUserNotFound
	}

	var result []*model.PvpRecord

	err := database.Where("self_uid=? ", uid).Find(&result)
	if err != nil {
		logger.Error(err)
		return nil, errutil.ErrDBOperation
	}
	return result, nil
}

// InsertPvpRecord 插入记录
func InsertPvpRecord(record *model.PvpRecord) error {
	_, err := database.Insert(record)
	if err != nil {
		logger.Error(err)
		return errutil.ErrDBOperation
	}
	return nil
}

// DeletePvpRecord 删除记录
func DeletePvpRecord(uid int64) error {
	_, err := database.Where("uid=?", uid).Delete(model.PvpRecord{})
	if err != nil {
		logger.Error(err)
		return errutil.ErrDBOperation
	}
	return nil
}
